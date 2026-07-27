using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LiteNetLib;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.Network;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务端主控类。使用 LiteEntitySystem 替代原有 JSON 消息系统。
/// 大厅/房间管理委托给 GameLobby 模块，战斗逻辑委托给 GameLogicService。
/// </summary>
public class GameServer {
    private readonly EntityNetworkServer _networkServer;
    private readonly GameLobby _lobby;
    private readonly IServerBattleService _battleService;
    private readonly GameLogicService _logicService;
    private Thread? _loopThread;

    private volatile bool _running;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private double _lastTickTime;

    public bool IsRunning => _running;
    private const double TickInterval = 0.05; // 20 Hz

    public GameServer() {
        _logicService = new GameLogicService();
        _battleService = _logicService; // GameLogicService 同时实现 IServerBattleService
        _networkServer = new EntityNetworkServer();
        _lobby = new GameLobby(_networkServer, _battleService);

        _networkServer.OnClientConnected += peerId =>
            Console.WriteLine($"[Game] Client {peerId} connected.");
        _networkServer.OnCustomPacket += OnCustomPacket;
        UnitSyncEntity.SkillCastRequested += OnSkillCastRequested;
    }

    public void StartAsync() {
        if (_running)
            return;
        _networkServer.Start();
        _running = true;
        _lastTickTime = _tickWatch.Elapsed.TotalSeconds;

        _loopThread = new Thread(RunLoop) { Name = "GameServer-MainLoop", IsBackground = true };
        _loopThread.Start();
        Console.WriteLine("[GameServer] Started (async mode)");
    }

    public void StartWithConsole() {
        if (_running)
            return;
        StartAsync();
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  DungeonChessBattle Server (LES Edition)");
        Console.WriteLine("  Type 'help' for commands.");
        Console.WriteLine("══════════════════════════════════════════");
        _lobby.RunConsoleLoop(() => _networkServer.PeerCount, () => _tickWatch.Elapsed);
        Stop();
    }

    public void Stop() {
        _running = false;
        _loopThread?.Join(TimeSpan.FromSeconds(3));
        Console.WriteLine($"Server stopped. Peers: {_networkServer.PeerCount}");
        _networkServer.Stop();
    }

    private void RunLoop() {
        while (_running) {
            double now = _tickWatch.Elapsed.TotalSeconds;
            double deltaTime = now - _lastTickTime;
            _networkServer.PollEvents();

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
        _networkServer.EntityManager.Update();

        foreach (var (roomId, syncUnits) in _lobby.RoomUnits) {
            if (!_lobby.Rooms.TryGetValue(roomId, out var roomEntity))
                continue;
            if (roomEntity.BattlePhase.Value == 0 || roomEntity.BattlePhase.Value == 4)
                continue;

            var gameRoom = _logicService.GetRoom(roomId);
            if (gameRoom == null)
                continue;

            // Entity → Logic: 外部 Health 变更写入 IUnitState
            GameLogicService.SyncHealthFromExternal(gameRoom,
                syncUnits.Select(s => (s.UnitName.Value, s.Health.Value)));

            // Buff 结算（通过接口调用）
            var battle = _battleService.GetBattle(roomId);
            if (battle != null) {
                _battleService.UpdateBuffs(battle,
                    gameRoom.UnitsA.Concat(gameRoom.UnitsB), deltaTime);
            }

            // 战斗结束检查（PhaseChanged 事件订阅负责写入 IsFinished/WinnerCamp）
            if (battle != null && _logicService.CheckBattleEnded(gameRoom)) {
                _battleService.EndBattle(battle);
            }

            // Logic → Entity: 结算后的 Health 写回 UnitSyncEntity
            var syncMap = syncUnits.ToDictionary(s => s.UnitName.Value);
            foreach (var unit in gameRoom.UnitsA.Concat(gameRoom.UnitsB)) {
                if (!syncMap.TryGetValue(unit.UnitStateName, out var syncUnit))
                    continue;
                if (MathF.Abs(syncUnit.Health.Value - unit.Health) > 0.0001f)
                    syncUnit.ServerSetHealth(unit.Health);
            }
        }
    }

    #region Custom Packet Handler

    private void OnCustomPacket(NetPeer peer, ReadOnlySpan<byte> data) {
        try {
            string json = Encoding.UTF8.GetString(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? type = root.GetProperty("type").GetString();

            switch (type) {
                case "start_battle":
                    HandleStartBattle(root);
                    break;
                case "advance_phase":
                    HandleAdvancePhase();
                    break;
                case "next_round":
                    HandleNextRound();
                    break;
                case "end_battle":
                    HandleEndBattle();
                    break;
                default:
                    Console.WriteLine($"[Game] Unknown command: {type}");
                    break;
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"[Game] Custom packet error: {ex.Message}");
        }
    }

    private void SubscribePhaseSync(BattleManager battle, BattleRoomEntity roomEntity) {
        battle.PhaseChanged += (prev, next) => {
            roomEntity.BattlePhase.Value = next switch {
                BattlePhase.PlayerTurn => 1,
                BattlePhase.SkillCasting => 2,
                BattlePhase.Finished => 4,
                _ => 0
            };
            roomEntity.CurrentRound.Value = (ushort)battle.RoundNumber;
            roomEntity.IsFinished.Value = next == BattlePhase.Finished;

            if (next == BattlePhase.Finished) {
                var gameRoom = _logicService.GetRoom(roomEntity.RoomId.Value);
                if (gameRoom != null && _logicService.CheckBattleEnded(gameRoom)) {
                    roomEntity.WinnerCamp.Value = (byte)(
                        BattleResolver.HasAliveUnits(gameRoom.UnitsA) ? 1u : 2u);
                }
            }
        };
    }

    private void HandleStartBattle(JsonElement root) {
        string? roomId = root.TryGetProperty("roomId", out var rp) ? rp.GetString() : null;
        var roomEntity = roomId != null && _lobby.Rooms.TryGetValue(roomId, out var r)
            ? r : _lobby.Rooms.Values.FirstOrDefault();
        if (roomEntity == null) {
            Console.WriteLine("[Game] No room to start battle.");
            return;
        }

        var battle = _battleService.StartBattleInRoom(roomEntity.RoomId.Value);
        SubscribePhaseSync(battle, roomEntity);

        Console.WriteLine($"[Game] Battle started in room: {roomEntity.RoomId.Value}");
    }

    private void HandleAdvancePhase() {
        var roomEntity = _lobby.Rooms.Values.FirstOrDefault(r => r.BattlePhase.Value is 1 or 2);
        if (roomEntity == null) {
            Console.WriteLine("[Game] No active battle to advance.");
            return;
        }

        var battle = _battleService.GetBattle(roomEntity.RoomId.Value);
        if (battle == null) {
            battle = _battleService.StartBattleInRoom(roomEntity.RoomId.Value);
            // 新创建的战斗实例也需要订阅同步事件
            SubscribePhaseSync(battle, roomEntity);
        }
        _battleService.AdvanceBattlePhase(battle);

        Console.WriteLine($"[Game] Phase advanced to {roomEntity.BattlePhase.Value} in room: {roomEntity.RoomId.Value}");
    }

    private void HandleNextRound() {
        var roomEntity = _lobby.Rooms.Values.FirstOrDefault(r => r.BattlePhase.Value is 1 or 2);
        if (roomEntity == null) {
            Console.WriteLine("[Game] No active battle for next round.");
            return;
        }

        var battle = _battleService.GetBattle(roomEntity.RoomId.Value);
        if (battle == null)
            return;

        _battleService.AdvanceBattlePhase(battle);
        // PhaseChanged 事件通过 SetPhaseSyncCallback 自动同步到 Entity

        Console.WriteLine($"[Game] Round {roomEntity.CurrentRound.Value} in room: {roomEntity.RoomId.Value}");
    }

    private void HandleEndBattle() {
        var roomEntity = _lobby.Rooms.Values.FirstOrDefault(r => r.BattlePhase.Value is not 0 and not 4);
        if (roomEntity == null) {
            Console.WriteLine("[Game] No active battle to end.");
            return;
        }

        var battle = _battleService.GetBattle(roomEntity.RoomId.Value);
        if (battle != null)
            _battleService.EndBattle(battle);
        // PhaseChanged → Finished 通过 SetPhaseSyncCallback 自动同步到 Entity

        Console.WriteLine($"[Game] Battle ended in room: {roomEntity.RoomId.Value}");
    }

    /// <summary>
    /// 处理通过 RPC 到达的技能施放请求。
    /// </summary>
    private void OnSkillCastRequested(UnitSyncEntity casterSync, SyncSkillRequest req) {
        var targetSync = _lobby.GetUnitById(req.TargetUnitNetId);
        if (targetSync == null) {
            Console.WriteLine($"[Game] Skill RPC: target unit {req.TargetUnitNetId} not found.");
            return;
        }

        var casterModel = _logicService.FindUnitModel(casterSync.UnitName.Value);
        var targetModel = _logicService.FindUnitModel(targetSync.UnitName.Value);
        if (casterModel == null || targetModel == null) {
            Console.WriteLine($"[Game] Skill RPC: unit model not found in Logic layer.");
            return;
        }

        var roomId = _lobby.FindRoomIdByUnit(casterSync);
        if (string.IsNullOrEmpty(roomId)) {
            Console.WriteLine("[Game] Skill RPC: room not found for caster.");
            return;
        }
        var battle = _battleService.GetBattle(roomId);
        if (battle == null) {
            Console.WriteLine("[Game] Skill RPC: no active battle in room.");
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

        targetSync.ServerSetHealth(targetModel.Health);

        Console.WriteLine($"[Game] Skill RPC result: {casterSync.UnitName.Value} -> {targetSync.UnitName.Value}, " +
                          $"HP: {oldTargetHealth:F0} -> {targetSync.Health.Value:F0}");
    }

    #endregion
}
