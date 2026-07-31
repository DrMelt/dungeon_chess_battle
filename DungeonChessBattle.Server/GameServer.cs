using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using LiteNetLib;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务端主控类。
/// 大厅端口 (10170) 处理 create_room / join_room 等 JSON 消息，
/// 每个房间拥有独立的端口 + ServerEntityManager 实现物理隔离。
/// </summary>
public class GameServer {
    private readonly LobbyNetworkServer _lobbyServer;
    private readonly GameLobby _lobby;
    private readonly IServerBattleService _battleService;
    private readonly GameLogicService _logicService;
    private readonly ILogger<GameServer> _logger;
    private Thread? _loopThread;

    private volatile bool _running;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private double _lastTickTime;

    public bool IsRunning => _running;
    private const double TickInterval = 0.016; // 60 Hz

    public GameServer(ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<GameServer>();
        _logicService = new GameLogicService();
        _battleService = _logicService; // GameLogicService 同时实现 IServerBattleService
        _lobbyServer = new LobbyNetworkServer(loggerFactory.CreateLogger<LobbyNetworkServer>());
        _lobby = new GameLobby(_battleService, loggerFactory);

        _lobbyServer.OnCustomPacket += OnCustomPacket;
        UnitPawn.SkillCastRequested += OnPawnSkillCastRequested;
        BattleRoomEntity.CreateUnitRequested += OnRoomCreateUnitRequested;
        BattleRoomEntity.StartBattleRequested += OnRoomStartBattleRequested;
    }

    public void StartAsync(int lobbyPort) {
        if (_running)
            return;
        _lobbyServer.Start(lobbyPort);
        _running = true;
        _lastTickTime = _tickWatch.Elapsed.TotalSeconds;

        _loopThread = new Thread(RunLoop) { Name = "GameServer-MainLoop", IsBackground = true };
        _loopThread.Start();
        _logger.LogInformation("[GameServer] Started (async mode, lobby port: {Port})", lobbyPort);
    }

    public void StartWithConsole() {
        if (_running)
            return;
        StartAsync(10170);
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  DungeonChessBattle Server (Multi-Room)");
        Console.WriteLine("  Type 'help' for commands.");
        Console.WriteLine("══════════════════════════════════════════");
        _lobby.RunConsoleLoop(() => _lobbyServer.PeerCount, () => _tickWatch.Elapsed);
        Stop();
    }

    public void Stop() {
        _running = false;
        _loopThread?.Join(TimeSpan.FromSeconds(3));

        // 停止所有房间服务器
        foreach (var (_, server) in _lobby.RoomServers)
            server.Stop();

        _lobbyServer.Stop();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Server stopped.");
    }

    private void RunLoop() {
        while (_running) {
            double now = _tickWatch.Elapsed.TotalSeconds;
            double deltaTime = now - _lastTickTime;
            _lobbyServer.PollEvents();

            // 驱动所有房间服务器的网络事件
            foreach (var (_, server) in _lobby.RoomServers)
                server.PollEvents();

            if (deltaTime >= TickInterval) {
                _lastTickTime = now;
                Tick(deltaTime);
                if (now - _lastTickTime > TickInterval * 2)
                    _lastTickTime = now;
            }
            Thread.Sleep(1);
        }
    }

    private void Tick(double deltaTime) {
        // 更新所有房间的 EntityManager
        foreach (var (_, server) in _lobby.RoomServers)
            server.EntityManager.Update();

        // 驱动每个房间的战斗逻辑
        foreach (var (roomId, pawns) in _lobby.RoomPawns) {
            if (!_lobby.Rooms.TryGetValue(roomId, out var roomEntity))
                continue;
            if ((BattlePhase)roomEntity.BattlePhase.Value != BattlePhase.Running)
                continue;

            var gameRoom = _logicService.GetRoom(roomId);
            var battle = _battleService.GetBattle(roomId);

            // 驱动战斗管理器 Tick
            if (battle != null) {
                _battleService.TickBattle(battle, (float)deltaTime);
                if (gameRoom != null)
                    _battleService.UpdateBuffs(battle,
                        gameRoom.UnitsA.Concat(gameRoom.UnitsB), deltaTime);
            }

            // 驱动 UnitPawn 实时逻辑（技能冷却 + Service→Entity Health 同步）
            foreach (var pawn in pawns) {
                pawn.UpdateCooldowns((float)deltaTime);

                // Service → Entity: Logic 层的血量变更写回 Pawn
                if (gameRoom != null) {
                    var model = gameRoom.UnitsA.Concat(gameRoom.UnitsB)
                        .FirstOrDefault(u => u.UnitStateName == pawn.UnitName.Value);
                    if (model != null && MathF.Abs(pawn.Health.Value - model.Health) > 0.0001f)
                        pawn.ServerSetHealth(model.Health);
                }
            }

            // 战斗结束检查
            if (battle != null && gameRoom != null && _logicService.CheckBattleEnded(gameRoom)) {
                _battleService.EndBattle(battle);
            }
        }
    }

    #region Custom Packet Handler (Lobby)

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

    private void SubscribePhaseSync(BattleManager battle, BattleRoomEntity roomEntity) {
        battle.BattleStarted += () => {
            roomEntity.BattlePhase.Value = (byte)BattlePhase.Running;
            roomEntity.IsFinished.Value = false;
        };

        battle.BattleEnded += () => {
            roomEntity.BattlePhase.Value = (byte)BattlePhase.Finished;
            roomEntity.IsFinished.Value = true;

            var gameRoom = _logicService.GetRoom(roomEntity.RoomId.Value);
            if (gameRoom != null && _logicService.CheckBattleEnded(gameRoom)) {
                roomEntity.WinnerCamp.Value = (byte)(
                    BattleResolver.HasAliveUnits(gameRoom.UnitsA) ? 1u : 2u);
            }
        };
    }

    private void HandleCreateRoom(NetPeer peer, JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        if (string.IsNullOrWhiteSpace(roomId)) {
            _logger.LogWarning("[Game] create_room: roomId is required.");
            return;
        }

        if (_lobby.Rooms.ContainsKey(roomId)) {
            _logger.LogWarning("[Game] Room '{RoomId}' already exists.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, false, "Room already exists."));
            return;
        }

        // 创建房间服务器（端口 + SEM + BattleRoomEntity）
        var (server, port) = _lobby.EnsureRoomServer(roomId);

        // 创建房间后也应将客户端重定向到房间端口
        SendToPeer(peer, MessageWriter.WriteJoinRoomRedirect(roomId, port));
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' created, client redirected to port {Port}.", roomId, port);
    }

    private void HandleJoinRoom(NetPeer peer, JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        if (string.IsNullOrWhiteSpace(roomId)) {
            _logger.LogWarning("[Game] join_room: roomId is required.");
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, null, false, "roomId is required."));
            return;
        }

        // 确保房间存在（不存在则创建，也允许直接加入已存在的房间）
        var (server, port) = _lobby.EnsureRoomServer(roomId);

        // 回发重定向响应（携带房间端口号）
        SendToPeer(peer, MessageWriter.WriteJoinRoomRedirect(roomId, port));
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Client redirected to room '{RoomId}' on port {Port}.", roomId, port);
    }

    #endregion

    #region RPC Event Handlers (跨房间事件，从各个 SEM 的 Entity 触发)

    /// <summary>
    /// 处理客户端通过 RPC 发来的创建单位请求。
    /// </summary>
    private void OnRoomCreateUnitRequested(BattleRoomEntity roomEntity, SyncCreateUnitRequest req) {
        string roomId = roomEntity.RoomId.Value;
        var roomServer = _lobby.GetRoomServer(roomId);
        if (roomServer == null) {
            _logger.LogWarning("[Game] CreateUnit RPC: room server not found for {RoomId}", roomId);
            return;
        }

        // 根据阵营计算默认出生点
        var spawnPos = req.Camp == 1
            ? new Vector2(0, 0)
            : new Vector2(5, 0);

        _lobby.CreatePawnEntity(roomServer, roomId, req.UnitName, req.Camp, spawnPos);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Unit created via RPC: {UnitName} in room {RoomId}, camp={Camp}",
                req.UnitName, roomId, req.Camp);
    }

    /// <summary>
    /// 处理客户端通过 RPC 发来的开始战斗请求。
    /// </summary>
    private void OnRoomStartBattleRequested(BattleRoomEntity roomEntity) {
        string roomId = roomEntity.RoomId.Value;
        var battle = _battleService.StartBattleInRoom(roomId);
        SubscribePhaseSync(battle, roomEntity);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Battle started via RPC in room: {RoomId}", roomId);
    }

    #endregion

    #region Skill & Helpers

    /// <summary>
    /// 处理通过 UnitPawn RPC 到达的技能施放请求。
    /// </summary>
    private void OnPawnSkillCastRequested(UnitPawn casterPawn, SyncSkillRequest req) {
        var targetPawn = FindPawnById(req.TargetUnitNetId);
        if (targetPawn == null) {
            _logger.LogWarning("[Game] Skill RPC: target pawn {TargetId} not found.", req.TargetUnitNetId);
            return;
        }

        var casterModel = _logicService.FindUnitModel(casterPawn.UnitName.Value);
        var targetModel = _logicService.FindUnitModel(targetPawn.UnitName.Value);
        if (casterModel == null || targetModel == null) {
            _logger.LogWarning("[Game] Skill RPC: unit model not found in Logic layer.");
            return;
        }

        var roomId = _lobby.FindRoomIdByPawn(casterPawn);
        if (string.IsNullOrEmpty(roomId)) {
            _logger.LogWarning("[Game] Skill RPC: room not found for caster.");
            return;
        }
        var battle = _battleService.GetBattle(roomId);
        if (battle == null) {
            _logger.LogWarning("[Game] Skill RPC: no active battle in room.");
            return;
        }

        float oldTargetHealth = targetModel.Health;

        if (req.IsDamage) {
            var skill = new SkillDamageModel {
                Damage = req.DamageOrCureValue,
                DamageType = (DungeonChessBattle.Core.Enums.Enum_DamageType)req.DamageType
            };
            _battleService.CastSkill(battle, casterModel, targetModel, skill);
        }
        else {
            var skill = new SkillCureModel { CurePotency = -req.DamageOrCureValue };
            _battleService.CastSkill(battle, casterModel, targetModel, skill);
        }

        targetPawn.ServerSetHealth(targetModel.Health);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Skill RPC result: {Caster} -> {Target}, HP: {OldHealth:F0} -> {NewHealth:F0}",
                casterPawn.UnitName.Value, targetPawn.UnitName.Value, oldTargetHealth, targetPawn.Health.Value);
    }

    /// <summary>
    /// 向指定客户端发送 JSON 消息。
    /// </summary>
    private static void SendToPeer(NetPeer peer, byte[] messageBytes) {
        peer.Send(messageBytes, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>
    /// 在所有 RoomPawns 中按 NetId 查找 UnitPawn。
    /// </summary>
    private UnitPawn? FindPawnById(ushort netId) {
        foreach (var (_, pawns) in _lobby.RoomPawns) {
            var match = pawns.Find(p => p.Id == netId);
            if (match != null)
                return match;
        }
        return null;
    }

    #endregion
}