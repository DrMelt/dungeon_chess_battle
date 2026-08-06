using DungeonChessBattle.Core.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// GameClientService 的连接连续性管理：房间重定向重连、断线自动重连、
/// 连接事件回调、后台更新循环与超时处理。
/// </summary>
public sealed partial class GameClientService {
    /// <summary>战斗启动重定向时暂存的 roomId（区别于加入房间重定向 _pendingJoinRoomId）。</summary>
    private string? _pendingBattleRoomId;

    // 房间重定向处理

    /// <summary>
    /// 重连到房间端口。大厅连接保持不断开。
    /// 由大厅重定向触发，用于切换到物理隔离的房间 SEM。
    /// 使用客户端持久 _playerId 作为连接密钥（P0-1：playerId 不从服务端回传）。
    /// </summary>
    /// <param name="host">服务器主机地址。</param>
    /// <param name="roomPort">房间端口。</param>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="isBattleStart">是否为战斗启动重定向（区别于加入房间重定向）。</param>
    private void ReconnectToRoom(string host, int roomPort, string roomId, bool isBattleStart = false) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("重连至房间端口: {Host}:{Port}, RoomId={RoomId}", host, roomPort, roomId);

        _reconnecting = true;
        try {
            Host = host;
            Port = roomPort;
            _cachedRoomPort = roomPort;
            _cachedRoomId = roomId;
            _connected = false;
            if (isBattleStart) {
                // 战斗启动重定向：连接成功后触发 OnBattleStarted，而非 OnRoomJoined
                _pendingBattleRoomId = roomId;
                _pendingJoinRoomId = null;
            }
            else {
                // 加入房间重定向：连接成功后桥接 OnRoomJoined
                _pendingJoinRoomId = roomId;
                _pendingBattleRoomId = null;
            }

            // 使用客户端持久 _playerId 作为连接密钥（服务端白名单验证）
            RoomClient.Reconnect(host, roomPort, PlayerId);
            _activeClient = RoomClient;

            _connectStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        catch (Exception ex) {
            _activeClient = null;
            _connected = false;
            _logger.LogError(ex, "重连至房间端口失败");
            ConnectionChanged?.Invoke(host, roomPort, false);
        }
        finally {
            _reconnecting = false;
        }
    }

    // 断线自动重连

    /// <summary>
    /// 当房间连接意外断开时，尝试通过大厅重新获取重定向。
    /// 如果大厅未连接，先建立连接，再通过事件驱动发送重连请求（避免竞态）。
    /// _reconnecting 覆盖从断线到重连成功的整个窗口。
    /// </summary>
    private void AttemptReconnectToRoom() {
        if (string.IsNullOrEmpty(_cachedRoomId)) {
            _logger.LogWarning("无法自动重连：缺少缓存的 roomId");
            return;
        }

        _reconnecting = true;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("尝试重连到房间 '{RoomId}' (playerId={PlayerId})...", _cachedRoomId, PlayerId);

        if (!LobbyClient.IsConnected) {
            // 事件驱动：等待大厅连接建立后再发送重连请求
            string connectionKey = _serverPassword ?? NetworkClientBase.ConnectionKey;
            void handler() {
                LobbyClient.OnFullyConnected -= handler;
                SendReconnectRequest();
            }

            LobbyClient.OnFullyConnected += handler;
            LobbyClient.Connect(Host, DefaultPort, connectionKey);
        }
        else {
            SendReconnectRequest(); // 大厅已连接，直接发送
        }
    }

    /// <summary>
    /// 发送重连请求到大厅（需确保大厅已连接）。
    /// </summary>
    private void SendReconnectRequest() {
        var cachedRoomId = _cachedRoomId ??
            throw new System.InvalidOperationException("cachedRoomId is not set before reconnect request.");
        var msg = MessageWriter.WriteReconnectRoom(
            cachedRoomId, PlayerId, PlayerName,
            _cachedRoomPassword, _serverPassword);
        LobbyClient.SendCommand(msg);
    }

    // 内部连接回调

    /// <summary>
    /// 连接成功回调：清除超时计时并通知状态变更。
    /// </summary>
    private void OnConnectionEstablished() {
        _connectStartTimestamp = 0;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("已连接到 {Host}:{Port}", Host, Port);
        ConnectionChanged?.Invoke(Host, Port, true);
    }

    /// <summary>
    /// 连接断开回调：通知状态变更。
    /// 更新循环由 Godot 主线程 GameClientDriver 驱动，断开时无需停止后台线程。
    /// </summary>
    private void OnConnectionLost() {
        if (LobbyClient.IsConnected || RoomClient.IsConnected) {
            _connected = LobbyClient.IsConnected || RoomClient.IsConnected;
            return;
        }

        _connected = false;

        if (_reconnecting) {
            return;
        }

        _logger.LogInformation("连接已断开");
        ConnectionChanged?.Invoke(Host, Port, false);
    }

    #region Update

    /// <summary>
    /// 连接超时处理：断开活动客户端并通知状态变更。
    /// </summary>
    private void HandleConnectionTimeout() {
        _logger.LogWarning("连接超时 ({Host}:{Port})", Host, Port);
        _connectStartTimestamp = 0;

        try {
            _activeClient?.Disconnect();
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "断开连接异常");
        }
        _activeClient = null;

        ConnectionChanged?.Invoke(Host, Port, false);
    }

    /// <summary>
    /// 每帧驱动大厅与房间客户端的网络轮询与 LES 实体更新，并监测连接超时。
    /// 由 Godot 主线程 GameClientDriver 节点在 _Process 中调用，
    /// 顺序位于 MainScene 输入采集之前（ProcessPriority 保证）。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public void Update(float delta) {
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

        if (!_connected && _connectStartTimestamp != 0) {
            double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_connectStartTimestamp).TotalSeconds;
            if (elapsed > ConnectTimeoutSeconds) {
                HandleConnectionTimeout();
            }
        }
    }

    #endregion
}
