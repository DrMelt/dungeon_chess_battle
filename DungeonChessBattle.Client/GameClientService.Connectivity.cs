using DungeonChessBattle.Client.Lobby;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 客户端连接状态机，单一事实源。取代散落的布尔与字段判断，
/// 使连接生命周期，大厅连接、房间连接与自动重连，可显式追踪并统一超时兜底。
/// </summary>
internal enum ClientConnectionState {
    /// <summary>未连接。</summary>
    Idle,

    /// <summary>正在连接大厅。</summary>
    ConnectingLobby,

    /// <summary>已连大厅，未进房间。</summary>
    InLobby,

    /// <summary>正在连接房间端口。</summary>
    ConnectingRoom,

    /// <summary>已进房间，准备阶段或战斗房间链路。</summary>
    InRoom,

    /// <summary>房间断开，经大厅自动重连中。</summary>
    Reconnecting,
}

/// <summary>
/// GameClientService 的连接连续性管理：连接状态机、重定向重连、断线自动重连、
/// 连接事件回调、超时处理与每帧驱动。
/// </summary>
public sealed partial class GameClientService {
    /// <summary>战斗启动重定向时暂存的 roomId，区别于加入房间重定向 _pendingJoinRoomId。</summary>
    private string? _pendingBattleRoomId;

    /// <summary>
    /// 状态机唯一转换入口：集中维护状态与超时时间戳。
    /// </summary>
    private void SetState(ClientConnectionState next) {
        if (_state != next) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("连接状态 {From} -> {To}", _state, next);
            _state = next;
        }
        _stateStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    // 房间重定向处理

    /// <summary>
    /// 重连到房间端口。大厅连接保持不断开。
    /// 由大厅重定向触发，用于切换到物理隔离的房间 SEM。
    /// 使用客户端持久 _playerId 作为连接密钥，P0-1：playerId 不从服务端回传。
    /// </summary>
    /// <param name="host">服务器主机地址。</param>
    /// <param name="roomPort">房间端口。</param>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="isBattleStart">是否为战斗启动重定向，区别于加入房间重定向。</param>
    private void ReconnectToRoom(string host, int roomPort, string roomId, bool isBattleStart = false) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("重连至房间端口: {Host}:{Port}, RoomId={RoomId}", host, roomPort, roomId);

        Host = host;
        Port = roomPort;
        _cachedRoomPort = roomPort;
        _cachedRoomId = roomId;
        if (isBattleStart) {
            // 战斗启动重定向：连接成功后触发 OnBattleStarted，而非 OnRoomJoined
            _pendingBattleRoomId = roomId;
            _pendingJoinRoomId = null;
        }
        else {
            // 加入房间重定向：连接成功后触发 OnRoomJoined
            _pendingJoinRoomId = roomId;
            _pendingBattleRoomId = null;
        }

        SetState(ClientConnectionState.ConnectingRoom);
        _activeClient = RoomClient;
        try {
            // 使用客户端持久 _playerId 作为连接密钥，服务端白名单验证
            RoomClient.Reconnect(host, roomPort, PlayerId);
        }
        catch (Exception ex) {
            _activeClient = null;
            SetState(ClientConnectionState.Idle);
            _logger.LogError(ex, "重连至房间端口失败");
            ConnectionChanged?.Invoke(host, roomPort, false);
        }
    }

    // 断线自动重连

    /// <summary>
    /// 当房间连接意外断开时，尝试通过大厅重新获取重定向。
    /// 如果大厅未连接，先建立连接，再通过事件驱动发送重连请求，避免竞态。
    /// 全程处于 <see cref="ClientConnectionState.Reconnecting"/>，失败/超时由
    /// <see cref="HandleConnectTimeout"/> 兜底复位，不会卡死。
    /// </summary>
    private void AttemptReconnectToRoom() {
        if (string.IsNullOrEmpty(_cachedRoomId)) {
            _logger.LogWarning("无法自动重连：缺少缓存的 roomId");
            ResetToNonRoomState();
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("尝试重连到房间 '{RoomId}' (playerId={PlayerId})...", _cachedRoomId, PlayerId);

        SetState(ClientConnectionState.Reconnecting);
        _activeClient = LobbyClient;

        if (!LobbyClient.IsConnected) {
            // 事件驱动：等待大厅连接建立后再发送重连请求
            void handler() {
                LobbyClient.OnFullyConnected -= handler;
                SendReconnectRequest();
            }

            LobbyClient.OnFullyConnected += handler;
            LobbyClient.Connect(Host, DefaultPort);
        }
        else {
            SendReconnectRequest(); // 大厅已连接，直接发送
        }
    }

    /// <summary>
    /// 发送重连请求到大厅，需确保大厅已连接。
    /// </summary>
    private void SendReconnectRequest() {
        var cachedRoomId = _cachedRoomId ??
            throw new InvalidOperationException("cachedRoomId is not set before reconnect request.");
        LobbyClient.RequestReconnectRoom(cachedRoomId, PlayerId, PlayerName, _cachedRoomPassword, _serverPassword);
    }

    // 内部连接回调

    /// <summary>
    /// 连接成功回调：通知状态变更。
    /// </summary>
    private void OnConnectionEstablished() {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("已连接到 {Host}:{Port}", Host, Port);
        ConnectionChanged?.Invoke(Host, Port, true);
    }

    /// <summary>
    /// 终结房间会话并复位状态机：清会话缓存与待办重定向、按当前所处状态复位，
    /// 原先处于房间会话（含重连中）时通知战斗编排层退出战斗。
    /// 重连失败、房间无缓存断开、连接超时与完全断开统一收敛至此，OnBattleSessionLost 仅由此触发。
    /// </summary>
    private void ResetToNonRoomState() {
        bool wasInRoom = _state is ClientConnectionState.ConnectingRoom
            or ClientConnectionState.InRoom
            or ClientConnectionState.Reconnecting;
        ClearRoomSessionCache();
        _pendingJoinRoomId = null;
        _pendingBattleRoomId = null;
        SetState(LobbyClient.IsConnected ? ClientConnectionState.InLobby : ClientConnectionState.Idle);
        if (wasInRoom)
            OnBattleSessionLost?.Invoke();
    }

    /// <summary>
    /// 完全断开回调：仅在大厅与房间都断开时视为完全断开并通知 UI。
    /// 更新循环由 Godot 主线程 GameClientDriver 驱动，断开时无需停止后台线程。
    /// </summary>
    private void OnConnectionLost() {
        if (LobbyClient.IsConnected || RoomClient.IsConnected)
            return;

        ResetToNonRoomState();
        _logger.LogInformation("连接已断开");
        ConnectionChanged?.Invoke(Host, Port, false);
    }

    #region Update

    /// <summary>
    /// 连接超时兜底：断开活动客户端并终结房间会话，清缓存、复位状态、按需通知退出战斗。
    /// 覆盖 连接大厅/连接房间/自动重连 三个进行中状态，杜绝卡死。
    /// </summary>
    private void HandleConnectTimeout() {
        if (_state is not (ClientConnectionState.ConnectingLobby
            or ClientConnectionState.ConnectingRoom
            or ClientConnectionState.Reconnecting))
            return;

        double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_stateStartTimestamp).TotalSeconds;
        if (elapsed <= ConnectTimeoutSeconds)
            return;

        _logger.LogWarning("连接超时 ({State}，{Elapsed}s)", _state, (int)elapsed);
        try {
            _activeClient?.Disconnect();
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "断开连接异常");
        }
        _activeClient = null;
        ResetToNonRoomState();
        ConnectionChanged?.Invoke(Host, Port, false);
    }

    /// <summary>
    /// 每帧驱动大厅与房间客户端的网络轮询与 LES 实体更新，并监测连接超时。
    /// 由 Godot 主线程 GameClientDriver 节点在 _Process 中调用，
    /// 顺序位于 MainScene 输入采集之前，ProcessPriority 保证。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public void Update(float delta) {
        // 先消费 SignalR 后台线程投递的动作，再驱动网络轮询。
        // 保证所有对 RoomClient 的操作都在主线程执行。
        while (_mainThreadActions.TryDequeue(out var action)) {
            try {
                action();
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "主线程动作执行异常");
            }
        }

        try {
            LobbyClient.Update(delta);
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "大厅客户端更新异常");
        }

        try {
            RoomClient.Update(delta);
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "房间客户端更新异常");
        }

        HandleConnectTimeout();
    }

    #endregion
}
