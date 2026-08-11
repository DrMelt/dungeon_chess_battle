using System.Collections.Concurrent;
using DungeonChessBattle.Protocol;
using DungeonChessBattle.Protocol.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client.Lobby;

/// <summary>
/// 大厅客户端，ASP.NET Core SignalR 版，负责与大厅端口的 LobbyHub 通信。
/// 处理 create_room、join_room、list_rooms、prepare_*、reconnect_room 请求及广播回调。
/// 公开请求方法与事件与旧 JSON 协议保持一致，供 UI 层与 GameClientService 复用。
/// 不包含 LES Entity 系统。
/// </summary>
public class LobbyClient(ILogger<LobbyClient> logger) : IClientConnection {
    private readonly ILogger<LobbyClient> _logger = logger;
    private readonly ConcurrentDictionary<string, RoomSnapshot> _roomSnapshots = new();
    private HubConnection? _hub;

    // 连接代际：每次 Connect 递增，用于隔离过期的异步 StartAsync 回调，
    // 防止旧连接建立成功后干扰新连接，配合旧连接释放。
    private int _connectionVersion;

    /// <summary>成功加入房间事件。参数：房间 ID。</summary>
    public event Action<string>? OnRoomJoined;

    /// <summary>成功创建房间事件。参数：房间 ID。</summary>
    public event Action<string>? OnRoomCreated;

    /// <summary>大厅重定向到房间端口事件。参数：房间 ID、端口。</summary>
    public event Action<string, int>? OnRedirectToRoom;

    /// <summary>重连失败事件。参数：错误信息。</summary>
    public event Action<string>? OnReconnectFailed;

    /// <summary>招募板房间列表接收事件。</summary>
    public event Action<List<RoomListing>>? OnRoomListReceived;

    /// <summary>准备阶段战斗启动重定向事件。参数：房间 ID、端口。</summary>
    public event Action<string, int>? OnPrepareBattleRedirect;

    /// <summary>房间快照更新事件，服务端组装单发。参数：房间 ID、完整快照。</summary>
    public event Action<string, RoomSnapshot>? OnRoomSnapshotUpdated;

    /// <summary>大厅完全连接成功事件。</summary>
    public event Action? OnFullyConnected;

    /// <summary>大厅连接完全关闭事件。</summary>
    public event Action? OnFullyDisconnected;

    /// <summary>当前是否已连接到大厅。</summary>
    public bool IsConnected => _hub is { State: HubConnectionState.Connected };

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

    /// <summary>SignalR 无需逐帧轮询；保留空实现以对齐旧驱动接口。</summary>
    public void Update(float delta) {
    }

    /// <summary>
    /// 构建 HubConnection 并注册广播回调与连接状态事件。
    /// </summary>
    private HubConnection CreateConnection(string host, int port) {
        var hub = new HubConnectionBuilder()
            .WithUrl($"http://{host}:{port}/lobby")
            .Build();

        hub.On<RoomSnapshot>(HubMethods.OnRoomSnapshot, HandleRoomSnapshot);
        hub.On<RoomRedirect>(HubMethods.OnPrepareBattleRedirect, r => OnPrepareBattleRedirect?.Invoke(r.RoomId, r.Port));
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
            _logger.LogWarning(ex, "[LobbyClient] 连接大厅失败");
            OnFullyDisconnected?.Invoke();
        }
    }

    /// <summary>后台执行异步请求并统一记录异常。</summary>
    private void FireAndForget(Func<Task> op) {
        _ = op().ContinueWith(t => {
            if (t.IsFaulted)
                _logger.LogWarning(t.Exception?.GetBaseException(), "[LobbyClient] 请求失败");
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
    /// 请求创建房间，含招募板配置。
    /// </summary>
    public void RequestCreateRoom(string roomId, string playerName, string playerId,
        string? roomPassword, RoomConfigDto? config, string? serverPassword = null) {
        var dto = new CreateRoomRequest(roomId, playerId, playerName, roomPassword, config, serverPassword);
        RunHubCall(async hub => {
            var result = await hub.InvokeAsync<LobbyResult>(HubMethods.CreateRoom, dto);
            if (result.Success) {
                OnRoomCreated?.Invoke(result.RoomId);
            }
            else if (_logger.IsEnabled(LogLevel.Warning)) {
                _logger.LogWarning("[LobbyClient] 创建房间失败: {Error}", result.Error);
            }
        });
    }

    /// <summary>
    /// 请求加入房间。
    /// </summary>
    public void RequestJoinRoom(string roomId, string playerName, string playerId,
        string? roomPassword, string? serverPassword = null) {
        var dto = new JoinRoomRequest(roomId, playerId, playerName, roomPassword, serverPassword);
        RunHubCall(async hub => {
            var result = await hub.InvokeAsync<LobbyResult>(HubMethods.JoinRoom, dto);
            if (result.Success) {
                OnRoomJoined?.Invoke(result.RoomId);
            }
            else if (_logger.IsEnabled(LogLevel.Warning)) {
                _logger.LogWarning("[LobbyClient] 加入房间失败: {Error}", result.Error);
            }
        });
    }

    /// <summary>
    /// 请求房间列表，招募板。
    /// </summary>
    public void RequestListRooms() {
        RunHubCall(async hub => {
            var result = await hub.InvokeAsync<RoomListResult>(HubMethods.ListRooms);
            OnRoomListReceived?.Invoke([.. result.Rooms]);
        });
    }

    /// <summary>
    /// 请求添加准备阶段单位。
    /// </summary>
    public void RequestPrepareAddUnit(string roomId, string unitName, string camp) {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.AddPrepareUnit,
                new PrepareAddUnitRequest(roomId, unitName, camp));
        });
    }

    /// <summary>
    /// 请求移除准备阶段单位。
    /// </summary>
    public void RequestPrepareRemoveUnit(string roomId, string unitName, string camp) {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.RemovePrepareUnit,
                new PrepareRemoveUnitRequest(roomId, unitName, camp));
        });
    }

    /// <summary>
    /// 请求开始战斗，仅房主可发起，需其他玩家已全部准备。
    /// </summary>
    public void RequestPrepareStartBattle(string roomId, string playerName, string playerId) {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.StartBattle,
                new PrepareStartBattleRequest(roomId, playerId, playerName));
        });
    }

    /// <summary>
    /// 请求设置是否已准备，仅非房主。
    /// </summary>
    public void RequestSetReady(string roomId, string playerName, bool ready) {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.SetReady,
                new PrepareReadyStateRequest(roomId, playerName, ready));
        });
    }
    /// <summary>
    /// 请求准备，仅非房主。
    /// </summary>
    public void RequestPrepareReady(string roomId, string playerName) => RequestSetReady(roomId, playerName, true);

    /// <summary>
    /// 请求取消准备，仅非房主。
    /// </summary>
    public void RequestPrepareUnready(string roomId, string playerName) => RequestSetReady(roomId, playerName, false);

    /// <summary>
    /// 请求重连房间。
    /// </summary>
    public void RequestReconnectRoom(string roomId, string playerId, string playerName,
        string? roomPassword, string? serverPassword = null) {
        var dto = new ReconnectRoomRequest(roomId, playerId, playerName, roomPassword, serverPassword);
        RunHubCall(async hub => {
            var result = await hub.InvokeAsync<LobbyResult>(HubMethods.ReconnectRoom, dto);
            if (result.Success && result.Port is > 0) {
                OnRedirectToRoom?.Invoke(result.RoomId, result.Port.Value);
            }
            else if (!result.Success) {
                OnReconnectFailed?.Invoke(result.Error ?? "Reconnect failed");
            }
        });
    }

    /// <summary>
    /// 请求离开房间，准备阶段主动退出。服务端从连接身份反查房间成员，无需传玩家名。
    /// </summary>
    public void RequestLeaveRoom(string roomId) {
        RunHubCall(async hub => {
            await hub.InvokeAsync<LobbyResult>(HubMethods.LeaveRoom, new LeaveRoomRequest(roomId));
        });
    }

    /// <summary>处理服务端广播的房间快照：缓存并触发更新事件。</summary>
    private void HandleRoomSnapshot(RoomSnapshot snapshot) {
        _roomSnapshots[snapshot.RoomId] = snapshot;
        OnRoomSnapshotUpdated?.Invoke(snapshot.RoomId, snapshot);
    }

    /// <summary>获取指定房间最近一次快照缓存，进房初始化用；不存在时返回 null。</summary>
    public RoomSnapshot? TryGetRoomSnapshot(string roomId) {
        _roomSnapshots.TryGetValue(roomId, out var snapshot);
        return snapshot;
    }

    /// <summary>断开/重连时清理房间快照缓存。</summary>
    private void ClearCaches() {
        _roomSnapshots.Clear();
    }
}
