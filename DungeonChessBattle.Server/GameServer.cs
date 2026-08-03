using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LiteNetLib;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务端主控类。
/// 大厅端口 (10170) 处理 create_room / join_room / reconnect_room 等 JSON 消息，
/// 每个房间拥有独立的端口 + 线程，实现完整的房间隔离。
/// GameServer 仅负责大厅消息路由和房间服务器生命周期协调。
/// 支持服务器密码 + 房间密码两层访问控制。
/// </summary>
public class GameServer {
    private readonly LobbyNetworkServer _lobbyServer;
    private readonly GameLobby _lobby;
    private readonly ILogger<GameServer> _logger;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private readonly string? _serverPassword;

    private volatile bool _running;
    private Thread? _lobbyThread;

    public bool IsRunning => _running;

    public GameServer(ILoggerFactory loggerFactory, string? serverPassword = null) {
        _logger = loggerFactory.CreateLogger<GameServer>();
        _serverPassword = string.IsNullOrEmpty(serverPassword) ? null : serverPassword;
        _lobbyServer = new LobbyNetworkServer(loggerFactory.CreateLogger<LobbyNetworkServer>(), _serverPassword);
        _lobby = new GameLobby(loggerFactory);

        _lobbyServer.OnCustomPacket += OnCustomPacket;
    }

    // ── 生命周期 ──────────────────────────────────────────

    public void StartAsync(int lobbyPort) {
        if (_running)
            return;
        _lobbyServer.Start(lobbyPort);
        _running = true;

        // 后台线程持续轮询大厅 NetManager（否则 create_room / join_room 永不处理）
        _lobbyThread = new Thread(() => {
            while (_running) {
                _lobbyServer.PollEvents();
                Thread.Sleep(1);
            }
        }) {
            Name = "Lobby-Poll", IsBackground = true
        };
        _lobbyThread.Start();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[GameServer] Started, lobby port: {Port}, ServerPassword={HasPassword}", lobbyPort, _serverPassword != null);
    }

    public void StartWithConsole() {
        if (_running)
            return;

        // 从环境变量读取服务器密码（如果未通过构造函数设置）
        var password = _serverPassword ?? Environment.GetEnvironmentVariable("DCB_SERVER_PASSWORD");
        if (!string.IsNullOrEmpty(password) && _serverPassword == null) {
            // 需要重建 LobbyNetworkServer 以使用密码
            _logger.LogWarning("[GameServer] Server password from env but LobbyNetworkServer already created without it. Restart required.");
        }

        StartAsync(10170);
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  DungeonChessBattle Server (Multi-Room)");
        Console.WriteLine("  Each room runs in its own thread.");
        Console.WriteLine($"  Server password: {(_serverPassword != null ? "ENABLED" : "DISABLED")}");
        Console.WriteLine("  Type 'help' for commands.");
        Console.WriteLine("══════════════════════════════════════════");

        // 大厅轮询已由 StartAsync 启动的后台线程独立驱动
        // 主线程仅低频检测键盘输入，避免 CLI 阻塞期间停止轮询
        while (_running) {
            if (Console.KeyAvailable) {
                _lobby.RunConsoleLoop(() => _lobbyServer.PeerCount, () => _tickWatch.Elapsed);
                break;
            }
            Thread.Sleep(50);
        }

        _running = false;
        Stop();
    }

    public void Stop() {
        _running = false;
        _lobbyThread?.Join(TimeSpan.FromSeconds(3));
        _lobby.StopAll();
        _lobbyServer.Stop();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Server stopped.");
    }

    // ── 大厅 JSON 消息处理 ───────────────────────────────

    private void OnCustomPacket(NetPeer peer, ReadOnlySpan<byte> data) {
        try {
            string json = Encoding.UTF8.GetString(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? type = root.GetProperty(MessageProperty.Type).GetString();

            switch (type) {
                case MessageType.CreateRoom:
                    HandleCreateRoom(peer, root);
                    break;
                case MessageType.JoinRoom:
                    HandleJoinRoom(peer, root);
                    break;
                case MessageType.ReconnectRoom:
                    HandleReconnectRoom(peer, root);
                    break;
                default:
                    _logger.LogWarning("[Game] Unknown command: {Type}", type);
                    break;
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[Game] Custom packet error");
        }
    }

    // ── 公共辅助方法（P2-6：消除三个 Handler 中的重复代码）──

    /// <summary>
    /// 验证服务器密码。失败时自动发送错误响应。
    /// </summary>
    /// <returns>密码验证通过返回 true</returns>
    private bool ValidateServerPassword(NetPeer peer, JsonElement root, string responseType, string? roomId) {
        string? serverPassword = root.TryGetProperty(MessageProperty.ServerPassword, out var sp) ? sp.GetString() : null;
        if (!string.IsNullOrEmpty(_serverPassword) && serverPassword != _serverPassword) {
            SendToPeer(peer, MessageWriter.WriteResponse(responseType, roomId, false, "Invalid server password."));
            return false;
        }
        return true;
    }

    /// <summary>
    /// 提取 roomId + playerId 并做非空校验。失败时自动发送错误响应。
    /// </summary>
    /// <returns>参数有效返回 true</returns>
    private bool TryGetRoomParams(NetPeer peer, JsonElement root, string responseType,
        out string roomId, out string playerId) {
        roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() ?? "" : "";
        playerId = root.TryGetProperty(MessageProperty.PlayerId, out var ip) ? ip.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId)) {
            _logger.LogWarning("[Game] {Type}: roomId and playerId are required.", responseType);
            SendToPeer(peer, MessageWriter.WriteResponse(responseType, roomId, false, "roomId and playerId are required."));
            return false;
        }
        return true;
    }

    /// <summary>
    /// 生成玩家显示名。
    /// </summary>
    private static string GetDisplayName(JsonElement root, string playerId) {
        string? playerName = root.TryGetProperty(MessageProperty.PlayerName, out var np) ? np.GetString() : null;
        return playerName ?? $"Player_{playerId[..Math.Min(playerId.Length, 6)]}";
    }

    /// <summary>
    /// 重定向客户端到房间端口并预注册玩家。
    /// </summary>
    private void RedirectToRoom(NetPeer peer, string roomId, string playerId, string displayName,
        RoomEntityServer server) {
        server.RegisterPlayer(playerId, displayName);
        SendToPeer(peer, MessageWriter.WriteJoinRoomRedirect(roomId, server.Port));
    }

    private void HandleCreateRoom(NetPeer peer, JsonElement root) {
        string roomId, playerId;
        if (!ValidateServerPassword(peer, root, MessageType.CreateRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.CreateRoomResponse, out roomId, out playerId))
            return;

        if (_lobby.GetRoom(roomId) != null) {
            _logger.LogWarning("[Game] Room '{RoomId}' already exists.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, false, "Room already exists."));
            return;
        }

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;

        var server = _lobby.CreateRoom(roomId, actualRoomPassword);
        string displayName = GetDisplayName(root, playerId);
        RedirectToRoom(peer, roomId, playerId, displayName, server);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' created, player='{Player}' ({PlayerId}), redirected to port {Port}.",
                roomId, displayName, playerId, server.Port);
    }

    private void HandleJoinRoom(NetPeer peer, JsonElement root) {
        string roomId, playerId;
        if (!ValidateServerPassword(peer, root, MessageType.JoinRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.JoinRoomResponse, out roomId, out playerId))
            return;

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;
        if (!_lobby.ValidateRoomPassword(roomId, actualRoomPassword)) {
            _logger.LogWarning("[Game] join_room: invalid password for room '{RoomId}'.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, false, "Invalid room password."));
            return;
        }

        var server = _lobby.CreateRoom(roomId);
        string displayName = GetDisplayName(root, playerId);
        RedirectToRoom(peer, roomId, playerId, displayName, server);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{Player}' ({PlayerId}) redirected to room '{RoomId}' on port {Port}.",
                displayName, playerId, roomId, server.Port);
    }

    private void HandleReconnectRoom(NetPeer peer, JsonElement root) {
        string roomId, playerId;
        if (!ValidateServerPassword(peer, root, MessageType.ReconnectRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.ReconnectRoomResponse, out roomId, out playerId))
            return;

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;
        if (!_lobby.ValidateRoomPassword(roomId, actualRoomPassword)) {
            _logger.LogWarning("[Game] reconnect_room: invalid password for room '{RoomId}'.", roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Invalid room password."));
            return;
        }

        var server = _lobby.GetRoom(roomId);
        if (server == null) {
            _logger.LogWarning("[Game] reconnect_room: room '{RoomId}' not found.", roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Room not found or expired."));
            return;
        }

        if (!server.CanReconnect(playerId)) {
            _logger.LogWarning("[Game] reconnect_room: player '{PlayerId}' not eligible for reconnect in room '{RoomId}'.",
                playerId, roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Reconnect timeout or player not in room."));
            return;
        }

        string? playerName = root.TryGetProperty(MessageProperty.PlayerName, out var np) ? np.GetString() : null;
        if (!string.IsNullOrEmpty(playerName))
            server.UpdatePlayerName(playerId, playerName);

        string displayName = GetDisplayName(root, playerId);
        RedirectToRoom(peer, roomId, playerId, displayName, server);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{PlayerName}' ({PlayerId}) reconnected to room '{RoomId}' on port {Port}.",
                playerName ?? "?", playerId, roomId, server.Port);
    }

    // ── 辅助 ──────────────────────────────────────────────

    private static void SendToPeer(NetPeer peer, byte[] messageBytes) {
        peer.Send(messageBytes, DeliveryMethod.ReliableOrdered);
    }
}