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
/// 大厅端口 (10170) 处理 create_room / join_room 等 JSON 消息，
/// 每个房间拥有独立的端口 + 线程，实现完整的房间隔离。
/// GameServer 仅负责大厅消息路由和房间服务器生命周期协调。
/// </summary>
public class GameServer {
    private readonly LobbyNetworkServer _lobbyServer;
    private readonly GameLobby _lobby;
    private readonly ILogger<GameServer> _logger;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();

    private volatile bool _running;
    private Thread? _lobbyThread;

    public bool IsRunning => _running;

    public GameServer(ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<GameServer>();
        _lobbyServer = new LobbyNetworkServer(loggerFactory.CreateLogger<LobbyNetworkServer>());
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
            _logger.LogInformation("[GameServer] Started, lobby port: {Port}", lobbyPort);
    }

    public void StartWithConsole() {
        if (_running)
            return;
        StartAsync(10170);
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  DungeonChessBattle Server (Multi-Room)");
        Console.WriteLine("  Each room runs in its own thread.");
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
                default:
                    _logger.LogWarning("[Game] Unknown command: {Type}", type);
                    break;
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[Game] Custom packet error");
        }
    }

    private void HandleCreateRoom(NetPeer peer, JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        if (string.IsNullOrWhiteSpace(roomId)) {
            _logger.LogWarning("[Game] create_room: roomId is required.");
            return;
        }

        if (_lobby.GetRoom(roomId) != null) {
            _logger.LogWarning("[Game] Room '{RoomId}' already exists.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, false, "Room already exists."));
            return;
        }

        // 创建房间服务器（独立线程）
        var server = _lobby.CreateRoom(roomId);

        // 客户端收到重定向后断连并连接到房间端口
        SendToPeer(peer, MessageWriter.WriteJoinRoomRedirect(roomId, server.Port));
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' created, client redirected to port {Port}.", roomId, server.Port);
    }

    private void HandleJoinRoom(NetPeer peer, JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        if (string.IsNullOrWhiteSpace(roomId)) {
            _logger.LogWarning("[Game] join_room: roomId is required.");
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, null, false, "roomId is required."));
            return;
        }

        // 确保房间存在（不存在则创建）
        var server = _lobby.CreateRoom(roomId);

        // 回发重定向响应
        SendToPeer(peer, MessageWriter.WriteJoinRoomRedirect(roomId, server.Port));
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Client redirected to room '{RoomId}' on port {Port}.", roomId, server.Port);
    }

    // ── 辅助 ──────────────────────────────────────────────

    private static void SendToPeer(NetPeer peer, byte[] messageBytes) {
        peer.Send(messageBytes, DeliveryMethod.ReliableOrdered);
    }
}
