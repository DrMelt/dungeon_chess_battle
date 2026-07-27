using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Rooms;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// Logic 层对外门面服务，实现 IBattleService。
/// 组合 RoomManager 和 BattleResolver，提供房间管理、战斗流程、技能结算等全部业务操作。
/// </summary>
public class GameLogicService : IBattleService {
    private readonly RoomManager _roomManager = new();
    private Action<string, byte, ushort, bool>? _phaseSyncCallback;

    /// <summary>
    /// 注入战斗阶段变化的回调，Logic 层通过此回调通知外部实体层同步 Phase/Round/Finished。
    /// 参数：roomId, phase, round, isFinished。
    /// </summary>
    public void SetPhaseSyncCallback(Action<string, byte, ushort, bool> callback) {
        _phaseSyncCallback = callback;
    }

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
        var model = new DungeonChessBattle.Core.Models.UnitModel { UnitStateName = unitName };
        if (camp == 1)
            room.UnitsA.Add(model);
        else if (camp == 2)
            room.UnitsB.Add(model);
        return model;
    }

    #endregion

    #region Battle Flow

    public BattleManager StartBattleInRoom(string roomId) {
        _ = GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");

        var battle = _roomManager.GetOrCreateBattle(roomId);
        battle.StartBattle();
        battle.PhaseChanged += OnBattlePhaseChanged;
        return battle;
    }

    public BattleManager? GetBattle(string roomId) => _roomManager.GetBattle(roomId);

    public void AdvanceBattlePhase(BattleManager battle) => battle.Advance();

    public void EndBattle(BattleManager battle) {
        battle.EndBattle();
        battle.PhaseChanged -= OnBattlePhaseChanged;
    }

    private void OnBattlePhaseChanged(BattlePhase prev, BattlePhase next) {
        foreach (var room in _roomManager.GetAllRooms()) {
            var battle = _roomManager.GetBattle(room.RoomId);
            if (battle == null || battle.CurrentPhase != next)
                continue;
            _phaseSyncCallback?.Invoke(
                room.RoomId,
                next switch {
                    BattlePhase.PlayerTurn => 1,
                    BattlePhase.SkillCasting => 2,
                    BattlePhase.Finished => 4,
                    _ => 0
                },
                (ushort)battle.RoundNumber,
                next == BattlePhase.Finished);
            break;
        }
    }

    #endregion

    #region Skill

    public void CastSkill(BattleManager battle, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null) {
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

    public void UpdateBuffs(BattleManager battle, IEnumerable<IUnitState> units, double deltaTime) {
        foreach (var unit in units) {
            BattleResolver.UpdateUnitBuffs(unit, deltaTime);
        }
    }

    public bool CheckBattleEnded(GameRoom room) {
        bool aAlive = BattleResolver.HasAliveUnits(room.UnitsA);
        bool bAlive = BattleResolver.HasAliveUnits(room.UnitsB);
        return !aAlive || !bAlive;
    }

    #endregion
}
