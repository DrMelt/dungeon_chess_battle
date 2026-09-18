using System.Collections.Concurrent;
using DungeonChessBattle.Lobby.Protocol;
using DungeonChessBattle.Lobby.Protocol.Dtos;
using DungeonChessBattle.Session.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Lobby.Client;

/// <summary>
/// 大厅客户端，ASP.NET Core SignalR 版，负责与大厅端口的 LobbyHub 通信。
/// 处理 create_room、join_room、list_rooms、prepare_*、reconnect_room 请求及广播回调。
/// 公开请求方法与事件与旧 JSON 协议保持一致，供 UI 层与 GameClientService 复用。
/// 登录成功时留存服务端签发的会话凭证（<see cref="SessionToken"/>）：凭证让身份可以延伸到
/// 服务端 HTTP 端点，本类不认识它的消费方是谁，回放等业务据此与大厅连接解耦。
/// 不包含 LES Entity 系统。
/// </summary>
public class LobbyClient(ILogger<LobbyClient> logger) {
    private readonly ILogger<LobbyClient> _logger = logger;
    private readonly ConcurrentDictionary<RoomId, RoomSnapshot> _roomSnapshots = new();
    private HubConnection? _hub;

    // 连接代际：每次 Connect 递增，用于隔离过期的异步 StartAsync 回调，
    // 防止旧连接建立成功后干扰新连接，配合旧连接释放。
    private int _connectionVersion;

    /// <summary>成功加入房间事件，参数为房间 ID。</summary>
    public event Action<RoomId>? OnRoomJoined;

    /// <summary>成功创建房间事件，参数为房间 ID。</summary>
    public event Action<RoomId>? OnRoomCreated;

    /// <summary>大厅重定向到房间端口事件。</summary>
    public event Action<RoomRedirect>? OnRedirectToRoom;

    /// <summary>重连失败事件，参数为失败原因。</summary>
    public event Action<string>? OnReconnectFailed;

    /// <summary>招募板房间列表接收事件。</summary>
    public event Action<IReadOnlyList<RoomListing>>? OnRoomListReceived;

    /// <summary>准备阶段战斗启动重定向事件。</summary>
    public event Action<RoomRedirect>? OnPrepareBattleRedirect;

    /// <summary>房间快照更新事件，服务端组装单发。</summary>
    public event Action<RoomSnapshot>? OnRoomSnapshotUpdated;

    /// <summary>大厅完全连接成功事件。</summary>
    public event Action? OnFullyConnected;

    /// <summary>登入结果事件。</summary>
    public event Action<LoginResult>? OnLoginResult;

    /// <summary>大厅连接完全关闭事件。</summary>
    public event Action? OnFullyDisconnected;

    /// <summary>当前是否已连接到大厅。</summary>
    public bool IsConnected => _hub is { State: HubConnectionState.Connected };

    /// <summary>服务端签发的会话凭证：登录成功时写入，断开与重连时清空；未登录时为 null。</summary>
    public string? SessionToken {
        get; private set;
    }

    /// <summary>
    /// 连接大厅，SignalR。
    /// </summary>
    public void Connect(string host, int port) {
        // 若已有旧连接，先释放，不触发 OnFullyDisconnected，避免与新连接状态串扰
        var old = _hub;
        _hub = null;
        if (old != null) {
            old.Closed -= OnClosed;
            _ = old.DisposeAsync().AsTask();
        }

        int version = ++_connectionVersion;
        // 新连接尚未登录，旧会话凭证作废
        SessionToken = null;
        var hub = CreateConnection(host, port);
        _hub = hub;
        _ = StartAsync(hub, version);
    }

    /// <summary>复用当前实例重连到新地址，先清理旧连接与缓存，再建立新连接。</summary>
    public void Reconnect(string host, int port) {
        ClearCaches();
        Connect(host, port);
    }

    /// <summary>断开与大厅的连接并清理状态。</summary>
    public void Disconnect() {
        var hub = _hub;
        _hub = null;
        ClearCaches();
        if (hub != null) {
            hub.Closed -= OnClosed;
            _ = hub.DisposeAsync().AsTask();
        }
        OnFullyDisconnected?.Invoke();
    }

    /// <summary>
    /// 构建 HubConnection 并注册广播回调与连接状态事件。
    /// </summary>
    private HubConnection CreateConnection(string host, int port) {
#pragma warning disable S5332 // 局域网大厅信令，开发环境不需要 TLS
        var hub = new HubConnectionBuilder()
            .WithUrl($"http://{host}:{port}{HubPaths.Lobby}")
            .Build();
#pragma warning restore S5332

        hub.On<RoomSnapshot>(HubMethods.OnRoomSnapshot, HandleRoomSnapshot);
        hub.On<RoomRedirect>(HubMethods.OnPrepareBattleRedirect, r => OnPrepareBattleRedirect?.Invoke(r));
        hub.Closed += OnClosed;
        return hub;
    }

    /// <summary>连接关闭回调。</summary>
    private Task OnClosed(Exception? _) {
        OnFullyDisconnected?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>异步启动连接，成功后触发 OnFullyConnected。</summary>
    private async Task StartAsync(HubConnection hub, int version) {
        try {
            await hub.StartAsync();
            if (version != _connectionVersion)
                return; // 连接已被更新取代，忽略过期回调
            OnFullyConnected?.Invoke();
        }
        catch (Exception ex) {
            if (version != _connectionVersion)
                return;
            _logger.LogWarning(ex, "连接大厅失败");
            OnFullyDisconnected?.Invoke();
        }
    }

    /// <summary>后台执行异步请求并统一记录异常。</summary>
    private void FireAndForget(Func<Task> op) {
        _ = op().ContinueWith(t => {
            if (t.IsFaulted)
                _logger.LogWarning(t.Exception?.GetBaseException(), "请求失败");
        });
    }

    /// <summary>发送请求到大厅，fire-and-forget，结果经事件回调。</summary>
    private void RunHubCall(Func<HubConnection, Task> op) {
        var hub = _hub;
        if (hub is not { State: HubConnectionState.Connected })
            return;
        FireAndForget(() => op(hub));
    }

    /// <summary>
    /// 请求登入大厅，登记服务端权威玩家名。连接建立后调用，重连后需重新登入。
    /// </summary>
    public void RequestLogin(string playerName) {
        RunHubCall(async hub => {
            var result = await hub.InvokeAsync<LoginResult>(HubMethods.Login, new LoginRequest(playerName));
            if (result.Success)
                SessionToken = result.SessionToken;
            OnLoginResult?.Invoke(result);
        });
    }

    /// <summary>
    /// 请求创建房间，房间 ID 由服务端生成并回传。
    /// </summary>
    public void RequestCreateRoom(string playerId,
        string? roomPassword, RoomConfigDto config, string? serverPassword = null) {
        var dto = new CreateRoomRequest(playerId, roomPassword, config, serverPassword);
        RunHubCall(async hub => {
            var result = await hub.InvokeAsync<LobbyResult>(HubMethods.CreateRoom, dto);
            if (!result.Success) {
                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("创建房间失败: {Error}", result.Error);
                return;
            }
            // 回包房间标识先过值对象判定，非法即不对外派发
            if (RoomId.TryCreate(result.RoomId) is not { } roomId) {
                _logger.LogWarning("创建房间回包房间标识非法：{RoomId}", result.RoomId);
                return;
            }
            OnRoomCreated?.Invoke(roomId);
        });
    }

    /// <summary>
    /// 请求加入房间。
    /// </summary>
    public void RequestJoinRoom(RoomId roomId, string playerId,
        string? roomPassword, string? serverPassword = null) {
        var dto = new JoinRoomRequest(roomId, playerId, roomPassword, serverPassword);
        RunHubCall(async hub => {
            var result = await hub.InvokeAsync<LobbyResult>(HubMethods.JoinRoom, dto);
            if (!result.Success) {
                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("加入房间失败: {Error}", result.Error);
                return;
            }
            // 回包房间标识先过值对象判定，非法即不对外派发
            if (RoomId.TryCreate(result.RoomId) is not { } roomId) {
                _logger.LogWarning("加入房间回包房间标识非法：{RoomId}", result.RoomId);
                return;
            }
            OnRoomJoined?.Invoke(roomId);
        });
    }

    /// <summary>
    /// 请求房间列表，招募板。
    /// </summary>
    public void RequestListRooms() {
        RunHubCall(async hub => {
            var result = await hub.InvokeAsync<RoomListResult>(HubMethods.ListRooms);
            OnRoomListReceived?.Invoke(result.Rooms);
        });
    }

    /// <summary>
    /// 请求添加准备阶段单位，房间由服务端从连接归属反查，阵营由副本配置按选项键解析。
    /// </summary>
    public void RequestPrepareAddUnit(string unitConfigKey, string campOptionKey) {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.AddPrepareUnit,
                new PrepareAddUnitRequest(unitConfigKey, campOptionKey));
        });
    }

    /// <summary>
    /// 请求移除准备阶段单位，房间由服务端从连接归属反查。
    /// </summary>
    public void RequestPrepareRemoveUnit(string unitConfigKey) {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.RemovePrepareUnit,
                new PrepareRemoveUnitRequest(unitConfigKey));
        });
    }

    /// <summary>
    /// 请求开始战斗，仅房主可发起，需其他玩家已全部准备。
    /// </summary>
    public void RequestPrepareStartBattle() {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.StartBattle);
        });
    }

    /// <summary>
    /// 请求设置是否已准备，仅非房主。
    /// </summary>
    public void RequestSetReady(bool ready) {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.SetReady,
                new PrepareReadyStateRequest(ready));
        });
    }
    /// <summary>
    /// 请求准备，仅非房主。
    /// </summary>
    public void RequestPrepareReady() => RequestSetReady(true);

    /// <summary>
    /// 请求取消准备，仅非房主。
    /// </summary>
    public void RequestPrepareUnready() => RequestSetReady(false);

    /// <summary>
    /// 请求重连房间。
    /// </summary>
    public void RequestReconnectRoom(RoomId roomId, string playerId,
        string? roomPassword, string? serverPassword = null) {
        var dto = new ReconnectRoomRequest(roomId, playerId, roomPassword, serverPassword);
        RunHubCall(async hub => {
            var result = await hub.InvokeAsync<LobbyResult>(HubMethods.ReconnectRoom, dto);
            if (result.Success && result.Port is > 0) {
                OnRedirectToRoom?.Invoke(new RoomRedirect(result.RoomId, result.Port.Value));
            }
            else if (!result.Success) {
                OnReconnectFailed?.Invoke(result.Error ?? "Reconnect failed");
            }
        });
    }

    /// <summary>
    /// 请求离开房间，准备阶段主动退出。
    /// </summary>
    public void RequestLeaveRoom() {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.LeaveRoom);
        });
    }

    /// <summary>处理服务端广播的房间快照：缓存并触发更新事件；房间标识非法即丢弃。</summary>
    private void HandleRoomSnapshot(RoomSnapshot snapshot) {
        // 广播房间标识先过值对象判定，非法即不进缓存也不派发
        if (RoomId.TryCreate(snapshot.RoomId) is not { } roomId) {
            _logger.LogWarning("丢弃房间标识非法的快照：{RoomId}", snapshot.RoomId);
            return;
        }
        _roomSnapshots[roomId] = snapshot;
        OnRoomSnapshotUpdated?.Invoke(snapshot);
    }

    /// <summary>获取指定房间最近一次快照缓存，进房初始化用；不存在时返回 null。</summary>
    public RoomSnapshot? TryGetRoomSnapshot(RoomId roomId) {
        _roomSnapshots.TryGetValue(roomId, out var snapshot);
        return snapshot;
    }

    /// <summary>断开/重连时清理房间快照缓存与会话凭证。</summary>
    private void ClearCaches() {
        _roomSnapshots.Clear();
        SessionToken = null;
    }
}
