using System.Text;
using System.Text.Json;
using DungeonChessBattle.Core.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 大厅客户端，负责与大厅端口 (10170) 的 JSON 协议通信。
/// 处理 create_room、join_room、reconnect_room 请求及对应的响应/重定向。
/// 不包含 LES Entity 系统。
/// </summary>
public class LobbyClient(ILogger<LobbyClient> logger) : NetworkClientBase(logger) {

    // ── 房间操作事件 ──
    public event Action<string>? OnRoomJoined;
    public event Action<string>? OnRoomCreated;

    /// <summary>大厅重定向到房间端口 (roomId, port)</summary>
    public event Action<string, int>? OnRedirectToRoom;

    /// <summary>重连失败事件</summary>
    public event Action<string>? OnReconnectFailed;

    // ── 请求方法 ──

    public void RequestCreateRoom(string roomId) {
        SendCommand(MessageWriter.WriteRoomRequest(MessageType.CreateRoom, roomId));
    }

    /// <summary>由 GameClientService 内部调用，重定向后桥接 OnRoomJoined 事件。</summary>
    internal void TriggerRoomJoined(string roomId) {
        _pendingEventInvocations.Enqueue(() => OnRoomJoined?.Invoke(roomId));
    }

    public void RequestJoinRoom(string roomId) {
        SendCommand(MessageWriter.WriteRoomRequest(MessageType.JoinRoom, roomId));
    }

    // ── OnNetworkReceive ──

    protected override void OnNetworkReceiveInternal(ReadOnlySpan<byte> data) {
        HandleCustomPacket(data);
    }

    // ── JSON 协议处理 ──

    private void HandleCustomPacket(ReadOnlySpan<byte> data) {
        try {
            string json = Encoding.UTF8.GetString(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? type = root.TryGetProperty(MessageProperty.Type, out var tp) ? tp.GetString() : null;

            switch (type) {
                case MessageType.JoinRoomResponse:
                    HandleJoinRoomResponse(root);
                    break;
                case MessageType.JoinRoomRedirect:
                    HandleRedirectToRoom(root);
                    break;
                case MessageType.CreateRoomResponse:
                    HandleCreateRoomResponse(root);
                    break;
                case MessageType.ReconnectRoomResponse:
                    HandleReconnectRoomResponse(root);
                    break;
                default:
                    if (_logger.IsEnabled(LogLevel.Warning))
                        _logger.LogWarning("[LobbyClient] Unknown custom packet: {Type}", type);
                    break;
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "[LobbyClient] Custom packet parse error");
        }
    }

    private void HandleJoinRoomResponse(JsonElement root) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? error = root.TryGetProperty(MessageProperty.Error, out var ep) ? ep.GetString() : null;

        if (success && !string.IsNullOrEmpty(roomId)) {
            _pendingEventInvocations.Enqueue(() => OnRoomJoined?.Invoke(roomId));
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[LobbyClient] Join room succeeded: {RoomId}", roomId);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[LobbyClient] Join room failed: {Error}", error ?? "unknown");
        }
    }

    private void HandleRedirectToRoom(JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        int port = root.TryGetProperty(MessageProperty.Port, out var pp) && pp.TryGetInt32(out var p) ? p : 0;

        if (!string.IsNullOrEmpty(roomId) && port > 0) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[LobbyClient] Redirecting to room '{RoomId}' on port {Port}", roomId, port);
            _pendingEventInvocations.Enqueue(() => OnRedirectToRoom?.Invoke(roomId, port));
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[LobbyClient] Redirect failed: invalid roomId or port");
        }
    }

    private void HandleCreateRoomResponse(JsonElement root) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? error = root.TryGetProperty(MessageProperty.Error, out var ep) ? ep.GetString() : null;

        if (success && !string.IsNullOrEmpty(roomId)) {
            _pendingEventInvocations.Enqueue(() => OnRoomCreated?.Invoke(roomId));
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[LobbyClient] Create room succeeded: {RoomId}", roomId);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[LobbyClient] Create room failed: {Error}", error ?? "unknown");
        }
    }

    private void HandleReconnectRoomResponse(JsonElement root) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? error = root.TryGetProperty(MessageProperty.Error, out var ep) ? ep.GetString() : null;

        if (!success) {
            string errorMsg = error ?? "Reconnect failed";
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[LobbyClient] Reconnect failed: {Error}", errorMsg);
            _pendingEventInvocations.Enqueue(() => OnReconnectFailed?.Invoke(errorMsg));
        }
        // 成功时会走已有的 HandleRedirectToRoom 流程（服务端返回重定向）
    }
}