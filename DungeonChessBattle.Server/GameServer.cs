using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LiteNetLib;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务端主控类。
/// 大厅端口 (10170) 处理 create_room / join_room / list_rooms / prepare_* / start_battle 等 JSON 消息。
/// 准备阶段在大厅连接上完成（选单位等），战斗开始时才创建 RoomEntityServer 并重定向客户端。
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

        var password = _serverPassword ?? Environment.GetEnvironmentVariable("DCB_SERVER_PASSWORD");
        if (!string.IsNullOrEmpty(password) && _serverPassword == null) {
            _logger.LogWarning("[GameServer] Server password from env but LobbyNetworkServer already created without it. Restart required.");
        }

        StartAsync(10170);
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  DungeonChessBattle Server (Multi-Room)");
        Console.WriteLine("  Prepare phase stays in lobby.");
        Console.WriteLine($"  Server password: {(_serverPassword != null ? "ENABLED" : "DISABLED")}");
        Console.WriteLine("  Type 'help' for commands.");
        Console.WriteLine("══════════════════════════════════════════");

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
                case MessageType.ListRooms:
                    HandleListRooms(peer);
                    break;
                case MessageType.PrepareAddUnit:
                    HandlePrepareAddUnit(peer, root);
                    break;
                case MessageType.PrepareRemoveUnit:
                    HandlePrepareRemoveUnit(peer, root);
                    break;
                case MessageType.PrepareStartBattle:
                    HandlePrepareStartBattle(peer, root);
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

    // ── 公共辅助方法 ──────────────────────────────────────

    private bool ValidateServerPassword(NetPeer peer, JsonElement root, string responseType, string? roomId) {
        string? serverPassword = root.TryGetProperty(MessageProperty.ServerPassword, out var sp) ? sp.GetString() : null;
        if (!string.IsNullOrEmpty(_serverPassword) && serverPassword != _serverPassword) {
            SendToPeer(peer, MessageWriter.WriteResponse(responseType, roomId, false, "Invalid server password."));
            return false;
        }
        return true;
    }

    private bool TryGetRoomParams(NetPeer peer, JsonElement root, string responseType,
        out string roomId, out string playerId) {
        roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() ?? "" : "";
        playerId = root.TryGetProperty(MessageProperty.PlayerId, out var ip) ? ip.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(roomId)) {
            _logger.LogWarning("[Game] {Type}: roomId is required.", responseType);
            SendToPeer(peer, MessageWriter.WriteResponse(responseType, roomId, false, "roomId is required."));
            return false;
        }
        return true;
    }

    private static string GetDisplayName(JsonElement root, string playerId) {
        string? playerName = root.TryGetProperty(MessageProperty.PlayerName, out var np) ? np.GetString() : null;
        return playerName ?? $"Player_{playerId[..Math.Min(playerId.Length, 6)]}";
    }

    // ── create_room：仅注册，不重定向 ──────────────────────

    private void HandleCreateRoom(NetPeer peer, JsonElement root) {
        if (!ValidateServerPassword(peer, root, MessageType.CreateRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.CreateRoomResponse, out string roomId, out string playerId))
            return;

        if (_lobby.RoomExists(roomId)) {
            _logger.LogWarning("[Game] Room '{RoomId}' already exists.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, false, "Room already exists."));
            return;
        }

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;

        // 解析招募板配置
        GameRoom? config;
        if (root.TryGetProperty(MessageProperty.Config, out var configEl)) {
            config = new GameRoom(roomId) {
                Title = configEl.TryGetProperty(MessageProperty.Title, out var t) ? t.GetString() ?? "" : "",
                Description = configEl.TryGetProperty(MessageProperty.Description, out var d) ? d.GetString() ?? "" : "",
                Category = configEl.TryGetProperty(MessageProperty.Category, out var c) && c.TryGetByte(out var cb)
                    ? (RoomCategory)cb : RoomCategory.Casual,
                HostName = configEl.TryGetProperty(MessageProperty.HostName, out var hn) ? hn.GetString() ?? "" : "",
                MaxPlayers = configEl.TryGetProperty(MessageProperty.MaxPlayers, out var mp) && mp.TryGetInt32(out var mpv)
                    ? mpv : 2,
                CurrentPlayers = 1,
            };
        }
        else {
            // 无配置时使用默认值，房间标题用 roomId
            config = new GameRoom(roomId) {
                Title = roomId,
                HostName = GetDisplayName(root, playerId),
                MaxPlayers = 2,
                CurrentPlayers = 1,
            };
        }

        if (!_lobby.RegisterRoom(roomId, actualRoomPassword, config)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, false, "Failed to register room."));
            return;
        }

        // 注册创建者到房间 peer 列表
        _lobby.RegisterPeerToRoom(roomId, peer);

        // 准备阶段：不重定向，只返回成功
        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, true));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' created (prepare), player='{Player}' ({PlayerId}), Title={Title}.",
                roomId, config.HostName, playerId, config.Title);
    }

    // ── join_room：不重定向，只验证 ─────────────────────

    private void HandleJoinRoom(NetPeer peer, JsonElement root) {
        if (!ValidateServerPassword(peer, root, MessageType.JoinRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.JoinRoomResponse, out string roomId, out string playerId))
            return;

        if (!_lobby.RoomExists(roomId)) {
            _logger.LogWarning("[Game] join_room: room '{RoomId}' not found.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, false, "Room not found."));
            return;
        }

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;
        if (!_lobby.ValidateRoomPassword(roomId, actualRoomPassword)) {
            _logger.LogWarning("[Game] join_room: invalid password for room '{RoomId}'.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, false, "Invalid room password."));
            return;
        }

        _lobby.UpdatePlayerCount(roomId, _lobby.GetRoomConfig(roomId)?.CurrentPlayers + 1 ?? 1);
        _lobby.RegisterPeerToRoom(roomId, peer);

        // 准备阶段加入：不重定向，直接成功
        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, true));

        string displayName = GetDisplayName(root, playerId);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{Player}' ({PlayerId}) joined room '{RoomId}' (prepare).",
                displayName, playerId, roomId);
    }

    // ── list_rooms ───────────────────────────────────────

    private void HandleListRooms(NetPeer peer) {
        try {
            var rooms = _lobby.GetRoomListings();
            SendToPeer(peer, MessageWriter.WriteListRoomsResponse(rooms));
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Game] Sent listing of {Count} rooms.", rooms.Count);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[Game] Error generating room listing.");
        }
    }

    // ── 准备阶段：添加/移除单位 ──────────────────────────

    private void HandlePrepareAddUnit(NetPeer peer, JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? unitName = root.TryGetProperty(MessageProperty.UnitName, out var un) ? un.GetString() : null;
        byte camp = root.TryGetProperty(MessageProperty.Camp, out var cp) && cp.TryGetByte(out var cb) ? cb : (byte)1;

        if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(unitName)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareAddUnit, roomId, false, "roomId and unitName required."));
            return;
        }

        if (!_lobby.AddPrepareUnit(roomId, unitName, camp)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareAddUnit, roomId, false, "Room not found."));
            return;
        }

        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareAddUnit, roomId, true));

        // 广播更新给房间内所有玩家
        BroadcastPrepareUnitList(roomId);
    }

    private void HandlePrepareRemoveUnit(NetPeer peer, JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? unitName = root.TryGetProperty(MessageProperty.UnitName, out var un) ? un.GetString() : null;
        byte camp = root.TryGetProperty(MessageProperty.Camp, out var cp) && cp.TryGetByte(out var cb) ? cb : (byte)1;

        if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(unitName)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareRemoveUnit, roomId, false, "roomId and unitName required."));
            return;
        }

        bool removed = _lobby.RemovePrepareUnit(roomId, unitName, camp);
        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareRemoveUnit, roomId, removed,
            removed ? null : "Unit not found."));

        if (removed)
            BroadcastPrepareUnitList(roomId);
    }

    // ── prepare_start_battle：创建 RoomEntityServer + 重定向 ──

    private void HandlePrepareStartBattle(NetPeer peer, JsonElement root) {
        if (!TryGetRoomParams(peer, root, MessageType.PrepareStartBattleResponse, out string roomId, out string playerId))
            return;

        if (!_lobby.RoomExists(roomId)) {
            _logger.LogWarning("[Game] start_battle: room '{RoomId}' not found.", roomId);
            SendToPeer(peer, MessageWriter.WritePrepareStartBattleResponse(roomId, 0));
            return;
        }

        // 创建 RoomEntityServer 并迁移单位
        var server = _lobby.StartRoomBattle(roomId);
        string displayName = GetDisplayName(root, playerId);
        server.RegisterPlayer(playerId, displayName);

        // 发送重定向（含端口号）
        SendToPeer(peer, MessageWriter.WritePrepareStartBattleResponse(roomId, server.Port));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' battle started (port {Port}), player='{Player}' ({PlayerId}) redirected.",
                roomId, server.Port, displayName, playerId);
    }

    // ── 重连（战斗中断线后使用） ──────────────────────────

    private void HandleReconnectRoom(NetPeer peer, JsonElement root) {
        if (!ValidateServerPassword(peer, root, MessageType.ReconnectRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.ReconnectRoomResponse, out string roomId, out string playerId))
            return;

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;
        if (!_lobby.ValidateRoomPassword(roomId, actualRoomPassword)) {
            _logger.LogWarning("[Game] reconnect_room: invalid password for room '{RoomId}'.", roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Invalid room password."));
            return;
        }

        var server = _lobby.GetRoomServer(roomId);
        if (server == null) {
            _logger.LogWarning("[Game] reconnect_room: room '{RoomId}' not in battle.", roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Room not in battle or expired."));
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
        server.RegisterPlayer(playerId, displayName);
        SendToPeer(peer, MessageWriter.WriteJoinRoomRedirect(roomId, server.Port));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{PlayerName}' ({PlayerId}) reconnected to room '{RoomId}' on port {Port}.",
                playerName ?? "?", playerId, roomId, server.Port);
    }

    // ── 广播辅助 ──────────────────────────────────────────

    /// <summary>
    /// 将房间当前准备单位列表广播给该房间所有 peer。
    /// </summary>
    private void BroadcastPrepareUnitList(string roomId) {
        var units = _lobby.GetPrepareUnits(roomId);
        var peers = _lobby.GetRoomPeers(roomId);
        var msg = MessageWriter.WritePrepareUnitList(roomId, units);

        foreach (var p in peers)
            SendToPeer(p, msg);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Game] Broadcast prepare units to {PeerCount} peers in room '{RoomId}' ({UnitCount} units)",
                peers.Count, roomId, units.Count);
    }

    // ── 辅助 ──────────────────────────────────────────────

    private static void SendToPeer(NetPeer peer, byte[] messageBytes) {
        peer.Send(messageBytes, DeliveryMethod.ReliableOrdered);
    }
}
