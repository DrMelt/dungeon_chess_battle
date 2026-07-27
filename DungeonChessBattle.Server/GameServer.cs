using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LiteNetLib;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Entities;
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
    private readonly GameLogicService _logicService = new();
    private Thread? _loopThread;

    private volatile bool _running;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private double _lastTickTime;

    public bool IsRunning => _running;
    private const double TickInterval = 0.05; // 20 Hz

    public GameServer() {
        _networkServer = new EntityNetworkServer();
        _lobby = new GameLobby(_networkServer, _logicService);
        _networkServer.OnClientConnected += peerId =>
            Console.WriteLine($"[Game] Client {peerId} connected.");
        _networkServer.OnCustomPacket += OnCustomPacket;
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

            // 将 UnitSyncEntity 的当前状态同步到 Logic 层的 UnitModel
            SyncToModels(syncUnits, roomId);

            // 通过 Logic 层结算 Buff
            var gameRoom = _logicService.GetRoom(roomId);
            if (gameRoom != null) {
                var battle = _logicService.GetBattle(roomId);
                if (battle != null) {
                    _logicService.UpdateBuffs(battle,
                        gameRoom.UnitsA.Concat(gameRoom.UnitsB), deltaTime);
                }
            }

            // 检查战斗结束
            if (gameRoom != null && _logicService.CheckBattleEnded(gameRoom)) {
                roomEntity.BattlePhase.Value = 4;
                roomEntity.IsFinished.Value = true;
                uint winnerCamp = BattleResolver.HasAliveUnits(gameRoom.UnitsA) ? 1u : 2u;
                roomEntity.WinnerCamp.Value = (byte)winnerCamp;
            }

            // 将 Logic 层结算后的变化写回 UnitSyncEntity
            SyncFromModels(syncUnits, roomId);
        }
    }

    /// <summary>
    /// 每帧 Tick 开始时，将 UnitSyncEntity 的网络状态同步到 Logic 层的 UnitModel。
    /// </summary>
    private void SyncToModels(List<UnitSyncEntity> syncUnits, string roomId) {
        var gameRoom = _logicService.GetRoom(roomId);
        if (gameRoom == null)
            return;

        var modelMap = gameRoom.UnitsA.Concat(gameRoom.UnitsB)
            .ToDictionary(m => m.UnitStateName);

        foreach (var syncUnit in syncUnits) {
            if (modelMap.TryGetValue(syncUnit.UnitName.Value, out var model)) {
                // 回写客户端操作结果（如通过 CastSkill 外部修改的 Health）
                if (MathF.Abs(syncUnit.Health.Value - model.Health) > 0.0001f) {
                    model.Health = syncUnit.Health.Value;
                }
            }
        }
    }

    /// <summary>
    /// 每帧 Tick 结束时，将 UnitModel 经 Logic 结算后的变化写回 UnitSyncEntity。
    /// </summary>
    private void SyncFromModels(List<UnitSyncEntity> syncUnits, string roomId) {
        var gameRoom = _logicService.GetRoom(roomId);
        if (gameRoom == null)
            return;

        var modelMap = gameRoom.UnitsA.Concat(gameRoom.UnitsB)
            .ToDictionary(m => m.UnitStateName);

        foreach (var syncUnit in syncUnits) {
            if (modelMap.TryGetValue(syncUnit.UnitName.Value, out var model)) {
                if (MathF.Abs(syncUnit.Health.Value - model.Health) > 0.0001f) {
                    syncUnit.ServerSetHealth(model.Health);
                }
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
                case "cast_skill":
                    HandleCastSkill(root);
                    break;
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

    private void HandleCastSkill(JsonElement root) {
        string? casterName = root.GetProperty("casterName").GetString();
        string? targetName = root.GetProperty("targetName").GetString();

        if (casterName == null || targetName == null)
            return;

        var caster = _lobby.FindUnitByName(casterName);
        var target = _lobby.FindUnitByName(targetName);
        if (caster == null || target == null) {
            Console.WriteLine($"[Game] Skill request units not found: {casterName} -> {targetName}");
            return;
        }

        bool isDamage = root.TryGetProperty("isDamage", out var isDamageProp) && isDamageProp.GetBoolean();
        Console.WriteLine($"[Game] Skill cast: {casterName} -> {targetName} (isDamage={isDamage})");

        ExecuteSkill(caster, target, root);
    }

    private void HandleStartBattle(JsonElement root) {
        string? roomId = root.TryGetProperty("roomId", out var rp) ? rp.GetString() : null;
        var roomEntity = roomId != null && _lobby.Rooms.TryGetValue(roomId, out var r)
            ? r : _lobby.Rooms.Values.FirstOrDefault();
        if (roomEntity == null) {
            Console.WriteLine("[Game] No room to start battle.");
            return;
        }

        var battle = _logicService.StartBattleInRoom(roomEntity.RoomId.Value);
        roomEntity.BattlePhase.Value = 1;
        roomEntity.CurrentRound.Value = (ushort)battle.RoundNumber;
        roomEntity.IsFinished.Value = false;

        Console.WriteLine($"[Game] Battle started in room: {roomEntity.RoomId.Value}");
    }

    private void HandleAdvancePhase() {
        var roomEntity = _lobby.Rooms.Values.FirstOrDefault(r => r.BattlePhase.Value is 1 or 2 or 3);
        if (roomEntity == null) {
            Console.WriteLine("[Game] No active battle to advance.");
            return;
        }

        var battle = _logicService.GetBattle(roomEntity.RoomId.Value)
            ?? _logicService.StartBattleInRoom(roomEntity.RoomId.Value);
        _logicService.AdvancePhase(battle);

        roomEntity.BattlePhase.Value = battle.CurrentPhase switch {
            BattlePhase.PlayerTurn => 1,
            BattlePhase.SkillCasting => 2,
            BattlePhase.Settlement => 3,
            _ => roomEntity.BattlePhase.Value
        };

        Console.WriteLine($"[Game] Phase advanced to {roomEntity.BattlePhase.Value} in room: {roomEntity.RoomId.Value}");
    }

    private void HandleNextRound() {
        var roomEntity = _lobby.Rooms.Values.FirstOrDefault(r => r.BattlePhase.Value is 1 or 2 or 3);
        if (roomEntity == null) {
            Console.WriteLine("[Game] No active battle for next round.");
            return;
        }

        var battle = _logicService.GetBattle(roomEntity.RoomId.Value);
        if (battle == null)
            return;
        _logicService.NextRound(battle);

        roomEntity.CurrentRound.Value = (ushort)battle.RoundNumber;
        roomEntity.BattlePhase.Value = 1;

        Console.WriteLine($"[Game] Round {roomEntity.CurrentRound.Value} in room: {roomEntity.RoomId.Value}");
    }

    private void HandleEndBattle() {
        var roomEntity = _lobby.Rooms.Values.FirstOrDefault(r => r.BattlePhase.Value is not 0 and not 4);
        if (roomEntity == null) {
            Console.WriteLine("[Game] No active battle to end.");
            return;
        }

        var battle = _logicService.GetBattle(roomEntity.RoomId.Value);
        if (battle == null)
            return;
        _logicService.EndBattle(battle);

        roomEntity.BattlePhase.Value = 4;
        roomEntity.IsFinished.Value = true;

        Console.WriteLine($"[Game] Battle ended in room: {roomEntity.RoomId.Value}");
    }

    private void ExecuteSkill(UnitSyncEntity casterSync, UnitSyncEntity targetSync, JsonElement skillJson) {
        // 从 Logic 层获取已挂载的 UnitModel，避免临时构建
        var casterModel = _logicService.FindUnitModel(casterSync.UnitName.Value);
        var targetModel = _logicService.FindUnitModel(targetSync.UnitName.Value);

        if (casterModel == null || targetModel == null) {
            Console.WriteLine($"[Game] Skill execution failed: unit model not found in Logic layer.");
            return;
        }

        float oldTargetHealth = targetModel.Health;

        bool isDamage = skillJson.TryGetProperty("isDamage", out var isDamageProp) && isDamageProp.GetBoolean();
        var battle = _logicService.GetBattle(_lobby.FindRoomIdByUnit(casterSync) ?? "")
            ?? new BattleManager();

        if (isDamage) {
            float damage = skillJson.TryGetProperty("damage", out var d) ? d.GetSingle() : 100f;
            int damageTypeInt = skillJson.TryGetProperty("damageType", out var dt) ? dt.GetInt32() : 1;
            var damageType = (Core.Enums.Enum_DamageType)damageTypeInt;

            var skill = new SkillDamageModel { Damage = damage, DamageType = damageType };
            _logicService.CastSkill(battle, casterModel, targetModel, skill);
        }
        else {
            float cure = skillJson.TryGetProperty("cure", out var c) ? c.GetSingle() : 50f;
            var skill = new SkillCureModel { CurePotency = cure };
            _logicService.CastSkill(battle, casterModel, targetModel, skill);
        }

        // 写回网络同步实体
        targetSync.ServerSetHealth(targetModel.Health);

        Console.WriteLine($"[Game] Skill result: {casterSync.UnitName.Value} -> {targetSync.UnitName.Value}, " +
                          $"HP: {oldTargetHealth:F0} -> {targetSync.Health.Value:F0}");
    }

    #endregion
}
