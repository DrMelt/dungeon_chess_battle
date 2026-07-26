using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LiteNetLib;
using LiteEntitySystem;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.Network;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务端主控类。使用 LiteEntitySystem 替代原有 JSON 消息系统。
/// 大厅/房间管理委托给 GameLobby 模块。
/// </summary>
public class GameServer
{
    private readonly EntityNetworkServer _networkServer;
    private readonly GameLobby _lobby;
    private Thread? _loopThread;

    private volatile bool _running;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private double _lastTickTime;

    public bool IsRunning => _running;
    private const double TickInterval = 0.05; // 20 Hz

    public GameServer()
    {
        _networkServer = new EntityNetworkServer();
        _lobby = new GameLobby(_networkServer);
        _networkServer.OnClientConnected += peerId =>
            Console.WriteLine($"[Game] Client {peerId} connected.");
        _networkServer.OnCustomPacket += OnCustomPacket;
    }

    public void StartAsync()
    {
        if (_running) return;
        _networkServer.Start();
        _running = true;
        _lastTickTime = _tickWatch.Elapsed.TotalSeconds;

        _loopThread = new Thread(RunLoop) { Name = "GameServer-MainLoop", IsBackground = true };
        _loopThread.Start();
        Console.WriteLine("[GameServer] Started (async mode)");
    }

    public void StartWithConsole()
    {
        if (_running) return;
        StartAsync();
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  DungeonChessBattle Server (LES Edition)");
        Console.WriteLine("  Type 'help' for commands.");
        Console.WriteLine("══════════════════════════════════════════");
        _lobby.RunConsoleLoop(() => _networkServer.PeerCount, () => _tickWatch.Elapsed);
        Stop();
    }

    public void Stop()
    {
        _running = false;
        _loopThread?.Join(TimeSpan.FromSeconds(3));
        Console.WriteLine($"Server stopped. Peers: {_networkServer.PeerCount}");
        _networkServer.Stop();
    }

    private void RunLoop()
    {
        while (_running)
        {
            double now = _tickWatch.Elapsed.TotalSeconds;
            double deltaTime = now - _lastTickTime;
            _networkServer.PollEvents();

            if (deltaTime >= TickInterval)
            {
                _lastTickTime = now;
                Tick(deltaTime);
                if (now - _lastTickTime > TickInterval * 2)
                    _lastTickTime = now;
            }
            Thread.Sleep(1);
        }
    }

    private void Tick(double deltaTime)
    {
        _networkServer.EntityManager.Update();

        // 遍历所有房间更新 Buff
        foreach (var (roomId, units) in _lobby.RoomUnits)
        {
            if (!_lobby.Rooms.TryGetValue(roomId, out var room)) continue;
            if (room.BattlePhase.Value == 0 || room.BattlePhase.Value == 4) continue;

            UpdateBuffs(units, (float)deltaTime);
            CheckBattleEnd(room, units);
        }
    }

    private static void UpdateBuffs(List<UnitSyncEntity> units, float deltaTime)
    {
        foreach (var unit in units)
        {
            if (unit.UnitState.Value != 0) continue;
            for (int i = unit.BuffsList.Count - 1; i >= 0; i--)
            {
                var buff = unit.BuffsList[i];
                buff.RemainingDuration -= deltaTime;

                if (buff.RemainingDuration <= 0)
                {
                    unit.ServerRemoveBuffAt(i);
                    continue;
                }

                if (buff.IsDOT)
                {
                    float tickDmg = buff.TickValue * deltaTime;
                    unit.ServerSetHealth(unit.Health.Value - tickDmg);
                }
                else if (buff.IsHOT)
                {
                    float tickHeal = Math.Abs(buff.TickValue) * deltaTime;
                    unit.ServerSetHealth(unit.Health.Value + tickHeal);
                }

                unit.BuffsList[i] = buff;
            }
        }
    }

    private static void CheckBattleEnd(BattleRoomEntity room, List<UnitSyncEntity> units)
    {
        bool aAlive = false, bAlive = false;
        foreach (var u in units)
        {
            if (u.UnitState.Value != 0) continue;
            if (u.Camp.Value == 1) aAlive = true;
            if (u.Camp.Value == 2) bAlive = true;
        }
        if (!aAlive) { room.BattlePhase.Value = 4; room.IsFinished.Value = true; room.WinnerCamp.Value = 2; }
        if (!bAlive) { room.BattlePhase.Value = 4; room.IsFinished.Value = true; room.WinnerCamp.Value = 1; }
    }

    #region Custom Packet Handler

    private void OnCustomPacket(NetPeer peer, ReadOnlySpan<byte> data)
    {
        try
        {
            string json = Encoding.UTF8.GetString(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? type = root.GetProperty("type").GetString();

            switch (type)
            {
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
        catch (Exception ex)
        {
            Console.WriteLine($"[Game] Custom packet error: {ex.Message}");
        }
    }

    private void HandleCastSkill(JsonElement root)
    {
        string? casterName = root.GetProperty("casterName").GetString();
        string? targetName = root.GetProperty("targetName").GetString();

        if (casterName == null || targetName == null) return;

        var caster = _lobby.FindUnitByName(casterName);
        var target = _lobby.FindUnitByName(targetName);
        if (caster == null || target == null)
        {
            Console.WriteLine($"[Game] Skill request units not found: {casterName} -> {targetName}");
            return;
        }

        bool isDamage = root.TryGetProperty("isDamage", out var isDamageProp) && isDamageProp.GetBoolean();
        Console.WriteLine($"[Game] Skill cast: {casterName} -> {targetName} (isDamage={isDamage})");

        ExecuteSkill(caster, target, root);
    }

    private void HandleStartBattle(JsonElement root)
    {
        string? roomId = root.TryGetProperty("roomId", out var rp) ? rp.GetString() : null;
        var room = roomId != null && _lobby.Rooms.TryGetValue(roomId, out var r) ? r : _lobby.Rooms.Values.FirstOrDefault();
        if (room == null) { Console.WriteLine("[Game] No room to start battle."); return; }
        room.BattlePhase.Value = 1;
        Console.WriteLine($"[Game] Battle started in room: {room.RoomId.Value}");
    }

    private void HandleAdvancePhase()
    {
        var room = _lobby.Rooms.Values.FirstOrDefault(r => r.BattlePhase.Value is 1 or 2 or 3);
        if (room == null) { Console.WriteLine("[Game] No active battle to advance."); return; }
        byte nextPhase = (byte)Math.Min(room.BattlePhase.Value + 1, 3);
        room.BattlePhase.Value = nextPhase;
        Console.WriteLine($"[Game] Phase advanced to {nextPhase} in room: {room.RoomId.Value}");
    }

    private void HandleNextRound()
    {
        var room = _lobby.Rooms.Values.FirstOrDefault(r => r.BattlePhase.Value is 1 or 2 or 3);
        if (room == null) { Console.WriteLine("[Game] No active battle for next round."); return; }
        room.CurrentRound.Value++;
        room.BattlePhase.Value = 1;
        Console.WriteLine($"[Game] Round {room.CurrentRound.Value} in room: {room.RoomId.Value}");
    }

    private void HandleEndBattle()
    {
        var room = _lobby.Rooms.Values.FirstOrDefault(r => r.BattlePhase.Value is not 0 and not 4);
        if (room == null) { Console.WriteLine("[Game] No active battle to end."); return; }
        room.BattlePhase.Value = 4;
        room.IsFinished.Value = true;
        Console.WriteLine($"[Game] Battle ended in room: {room.RoomId.Value}");
    }

    private void ExecuteSkill(UnitSyncEntity caster, UnitSyncEntity target, JsonElement skillJson)
    {
        var casterModel = BuildUnitModelFromEntity(caster);
        var targetModel = BuildUnitModelFromEntity(target);

        float oldTargetHealth = target.Health.Value;

        bool isDamage = skillJson.TryGetProperty("isDamage", out var isDamageProp) && isDamageProp.GetBoolean();
        if (isDamage)
        {
            float damage = skillJson.TryGetProperty("damage", out var d) ? d.GetSingle() : 100f;
            int damageTypeInt = skillJson.TryGetProperty("damageType", out var dt) ? dt.GetInt32() : 1;
            var damageType = (Core.Enums.Enum_DamageType)damageTypeInt;

            var skill = new SkillDamageModel { Damage = damage, DamageType = damageType };
            BattleResolver.ApplySkillDamage(casterModel, targetModel, skill);
        }
        else
        {
            float cure = skillJson.TryGetProperty("cure", out var c) ? c.GetSingle() : 50f;
            var skill = new SkillCureModel { CurePotency = cure };
            BattleResolver.ApplySkillCure(casterModel, targetModel, skill);
        }

        target.ServerSetHealth(targetModel.Health);

        Console.WriteLine($"[Game] Skill result: {caster.UnitName.Value} -> {target.UnitName.Value}, " +
                          $"HP: {oldTargetHealth:F0} -> {target.Health.Value:F0}");
    }

    private static UnitModel BuildUnitModelFromEntity(UnitSyncEntity e)
    {
        var model = new UnitModel
        {
            UnitStateName = e.UnitName.Value,
            Health = e.Health.Value,
            MaxHealth = e.MaxHealth.Value,
            PhysicalAttackBase = e.PhysicalAttackBase.Value,
            MagicAttackBase = e.MagicAttackBase.Value,
            PhysicalTakePercent = e.PhysicalTakePercent.Value,
            MagicTakePercent = e.MagicTakePercent.Value,
            CureIntensity = e.CureIntensity.Value,
            BaseSpeed = e.BaseSpeed.Value
        };
        return model;
    }

    #endregion
}