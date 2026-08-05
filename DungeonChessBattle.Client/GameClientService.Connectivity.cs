using DungeonChessBattle.Core.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// GameClientService 的连接连续性管理：房间重定向重连、断线自动重连、
/// 连接事件回调、后台更新循环与超时处理。
/// </summary>
public sealed partial class GameClientService {
    // 房间重定向处理

    /// <summary>
    /// 重连到房间端口。大厅连接保持不断开。
    /// 由大厅重定向触发，用于切换到物理隔离的房间 SEM。
    /// 使用客户端持久 _playerId 作为连接密钥（P0-1：playerId 不从服务端回传）。
    /// </summary>
    private void ReconnectToRoom(string host, int roomPort, string roomId) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("重连至房间端口: {Host}:{Port}, RoomId={RoomId}", host, roomPort, roomId);

        _reconnecting = true;
        try {
            Host = host;
            Port = roomPort;
            _cachedRoomPort = roomPort;
            _cachedRoomId = roomId;
            _connected = false;
            _pendingJoinRoomId = roomId;

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
    /// 连接断开回调：停止更新循环并通知状态变更。
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

        _running = false;
        if (_updateThread != null && Thread.CurrentThread != _updateThread) {
            _updateThread.Join(TimeSpan.FromSeconds(3));
            _updateThread = null;
        }
        _logger.LogInformation("连接已断开");
        ConnectionChanged?.Invoke(Host, Port, false);
    }

    #region Update Loop

    /// <summary>
    /// 启动后台更新循环（若未运行）。
    /// </summary>
    private void StartUpdateLoop() {
        if (_running)
            return;

        _running = true;
        _updateThread = new Thread(RunUpdate) {
            Name = "GameClient-Update",
            IsBackground = true,
        };
        _updateThread.Start();
    }

    /// <summary>
    /// 停止后台更新循环并等待线程退出。
    /// </summary>
    private void StopUpdateLoop() {
        _running = false;
        if (_updateThread != null && Thread.CurrentThread != _updateThread) {
            _updateThread.Join(TimeSpan.FromSeconds(3));
        }
        _updateThread = null;
    }

    /// <summary>
    /// 连接超时处理：断开活动客户端并通知状态变更。
    /// </summary>
    private void HandleConnectionTimeout() {
        _logger.LogWarning("连接超时 ({Host}:{Port})", Host, Port);
        _connectStartTimestamp = 0;
        _running = false;
        _updateThread = null;

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
    /// 后台更新循环：按 20Hz 驱动大厅与房间客户端的帧更新，并监测连接超时。
    /// </summary>
    private void RunUpdate() {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        double lastTick = 0;

        while (_running) {
            double now = watch.Elapsed.TotalSeconds;
            double delta = now - lastTick;

            if (delta >= TickInterval) {
                lastTick = now;
                try {
                    LobbyClient.Update((float)delta);
                    RoomClient.Update((float)delta);
                }
                catch (Exception ex) {
                    _logger.LogWarning(ex, "客户端更新异常");
                }

                if (!_connected && _connectStartTimestamp != 0) {
                    double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_connectStartTimestamp).TotalSeconds;
                    if (elapsed > ConnectTimeoutSeconds) {
                        HandleConnectionTimeout();
                        return;
                    }
                }
            }

            Thread.Sleep(1);
        }
    }

    #endregion
}
