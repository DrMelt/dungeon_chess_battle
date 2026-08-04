using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Rooms;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// Logic 层对外门面服务，同时实现 IServerBattleService 和 IClientBattleService。
/// 组合 RoomManager 和 BattleResolver，提供房间管理、战斗流程、技能结算等全部业务操作。
/// </summary>
public class GameLogicService : IServerBattleService, IClientBattleService {
    private readonly RoomManager _roomManager = new();

    // ── IClientBattleService 事件 ──
    public event Action<string, string, byte>? OnUnitCreated;
    public event Action<string, BattlePhase>? BattlePhaseChanged;
    public event Action<string, float, float>? UnitHealthChanged;
    public event Action<string>? UnitDied;
    public event Action<string, BuffEventData>? UnitBuffAdded;
    public event Action<string, BuffEventData>? UnitBuffRemoved;

    #region Room Management

    public GameRoom CreateRoom(string roomId) => _roomManager.CreateRoom(roomId);

    public GameRoom? GetRoom(string roomId) => _roomManager.GetRoom(roomId);

    public bool RemoveRoom(string roomId) => _roomManager.RemoveRoom(roomId);

    public IEnumerable<GameRoom> GetAllRooms() => _roomManager.GetAllRooms();

    /// <summary>
    /// 在指定房间创建 UnitModel 并加入对应阵营列表，返回 IUnitState 引用。
    /// 创建逻辑下沉到 Logic 层，避免外部模块直接实例化 Core.Models.UnitModel。
    /// </summary>
    public IUnitState CreateUnit(string roomId, string unitName, byte camp) {
        var room = _roomManager.GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");
        var model = new UnitModel {
            UnitStateName = unitName,
            Camps = [camp == 1 ? CampConstants.CampA : CampConstants.CampB],
        };
        if (camp == 1)
            room.UnitsA.Add(model);
        else
            room.UnitsB.Add(model);

        // 触发 OnUnitCreated 事件（本地模式同步回调）
        OnUnitCreated?.Invoke(roomId, unitName, camp);
        return model;
    }

    #endregion

    #region Battle Flow

    public BattleManager StartBattleInRoom(string roomId) {
        _ = GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");

        var battle = _roomManager.GetOrCreateBattle(roomId);
        battle.StartBattle();

        // 触发 BattlePhaseChanged 事件
        BattlePhaseChanged?.Invoke(roomId, BattlePhase.Running);
        return battle;
    }

    /// <summary>IClientBattleService 的请求开始战斗入口。</summary>
    void IClientBattleService.RequestStartBattle(string roomId) {
        StartBattleInRoom(roomId);
    }

    BattleManager IServerBattleService.StartBattleInRoom(string roomId) {
        return StartBattleInRoom(roomId);
    }

    BattleManager? IServerBattleService.GetBattle(string roomId) => _roomManager.GetBattle(roomId);

    void IServerBattleService.TickBattle(BattleManager battle, float deltaTime) => battle.Tick(deltaTime);

    public static void EndBattle(BattleManager battle) {
        battle.EndBattle();
    }

    void IServerBattleService.EndBattle(BattleManager battle) {
        battle.EndBattle();
    }

    #endregion

    #region Skill

    public static void CastSkill(BattleManager battle, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null) {
        _ = battle; // 接口兼容保留参数，实际结算不依赖 BattleManager 引用
        CastSkillInternal(caster, target, skill, allUnits);
    }

    void IServerBattleService.CastSkill(BattleManager battle, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits) {
        CastSkillInternal(caster, target, skill, allUnits);
    }

    void IClientBattleService.CastSkill(string roomId, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits) {
        _ = _roomManager.GetBattle(roomId)
            ?? throw new InvalidOperationException($"No active battle in room {roomId}.");
        CastSkillInternal(caster, target, skill, allUnits);
    }

    private static void CastSkillInternal(IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits) {
        switch (skill) {
            case SkillDamageModel damage:
                BattleResolver.ApplySkillDamage(caster, target, damage);
                break;
            case SkillCureModel cure:
                BattleResolver.ApplySkillCure(caster, target, cure);
                break;
            case SkillRangeDamageModel range:
                if (allUnits != null)
                    BattleResolver.ApplySkillRangeDamage(caster, allUnits, range);
                break;
            case SkillAddBuffModel addBuff:
                BattleResolver.ApplySkillAddBuff(target, addBuff);
                break;
        }
    }

    #endregion

    #region Unit Lookup & Sync

    public IUnitState? FindUnitModel(string unitName) {
        foreach (var room in _roomManager.GetAllRooms()) {
            var unit = room.UnitsA.Concat(room.UnitsB)
                .FirstOrDefault(u => u.UnitStateName == unitName);
            if (unit != null)
                return unit;
        }
        return null;
    }

    /// <summary>
    /// 将外部同步单元的 Health 写入 Logic 内部的 IUnitState 集合。
    /// </summary>
    public static void SyncHealthFromExternal(GameRoom room,
        IEnumerable<(string unitName, float health)> externalHealthValues) {
        var map = room.UnitsA.Concat(room.UnitsB)
            .ToDictionary(u => u.UnitStateName);
        foreach (var (name, health) in externalHealthValues) {
            if (map.TryGetValue(name, out var model))
                model.Health = health;
        }
    }

    /// <summary>
    /// 返回 Logic 结算后的 IUnitState Health 变化，供外部实体层写入。
    /// </summary>
    public static IEnumerable<(string unitName, float health)> SyncHealthToExternal(GameRoom room) {
        foreach (var unit in room.UnitsA.Concat(room.UnitsB))
            yield return (unit.UnitStateName, unit.Health);
    }

    #endregion

    #region Buffs & Status

    public static void UpdateBuffs(BattleManager battle, IEnumerable<IUnitState> units, double deltaTime) {
        _ = battle; // 接口兼容保留参数，实际结算不依赖 BattleManager 引用
        UpdateBuffsInternal(units, deltaTime);
    }

    void IServerBattleService.UpdateBuffs(BattleManager battle, IEnumerable<IUnitState> units, double deltaTime) {
        UpdateBuffsInternal(units, deltaTime);
    }

    void IClientBattleService.UpdateBuffs(string roomId, IEnumerable<IUnitState> units, double deltaTime) {
        UpdateBuffsInternal(units, deltaTime);
    }

    private static void UpdateBuffsInternal(IEnumerable<IUnitState> units, double deltaTime) {
        foreach (var unit in units) {
            BattleResolver.UpdateUnitBuffs(unit, deltaTime);
        }
    }

    public bool CheckBattleEnded(GameRoom room) {
        bool aAlive = BattleResolver.HasAliveUnits(room.UnitsA);
        bool bAlive = BattleResolver.HasAliveUnits(room.UnitsB);
        return !aAlive || !bAlive;
    }

    bool IClientBattleService.CheckBattleEnded(string roomId) {
        var room = GetRoom(roomId);
        return room != null && CheckBattleEnded(room);
    }

    /// <summary>IClientBattleService 的输入提交入口。本地模式无输入系统，空实现。</summary>
    void IClientBattleService.SubmitPlayerInput(float moveX, float moveY, byte skillFlags, float aimX, float aimY) {
        // 本地模式无输入系统
    }

    #endregion
}
