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
        var model = new DungeonChessBattle.Core.Models.UnitModel {
            UnitStateName = unitName,
            Camp = (Core.Enums.EnumCamp)camp,
        };
        if (camp == (byte)Core.Enums.EnumCamp.Camp_A)
            room.UnitsA.Add(model);
        else if (camp == (byte)Core.Enums.EnumCamp.Camp_B)
            room.UnitsB.Add(model);
        return model;
    }

    #endregion

    #region Battle Flow

    BattleManager IServerBattleService.StartBattleInRoom(string roomId) {
        _ = GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");

        var battle = _roomManager.GetOrCreateBattle(roomId);
        battle.StartBattle();
        return battle;
    }

    BattleManager? IServerBattleService.GetBattle(string roomId) => _roomManager.GetBattle(roomId);

    void IServerBattleService.AdvanceBattlePhase(BattleManager battle) => battle.Advance();

    void IServerBattleService.EndBattle(BattleManager battle) {
        battle.EndBattle();
    }

    #endregion

    #region Skill

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

    #endregion
}
