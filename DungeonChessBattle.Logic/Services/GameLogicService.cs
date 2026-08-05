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

    /// <summary>单位创建事件。参数：房间ID、单位名称、阵营(字符串)。</summary>
    public event Action<string, string, string>? OnUnitCreated;

    /// <summary>战斗阶段变化事件。参数：房间ID、战斗阶段。</summary>
    public event Action<string, BattlePhase>? BattlePhaseChanged;

#pragma warning disable CS0067 // 预留事件接口：由外部（如 MainScene）订阅，当前版本暂未在 Logic 内部触发
    /// <summary>单位生命值变化事件。参数：单位名称、新生命值、旧生命值。</summary>
    public event Action<string, float, float>? UnitHealthChanged;

    /// <summary>单位死亡事件。参数：单位名称。</summary>
    public event Action<string>? UnitDied;

    /// <summary>单位添加 Buff 事件。参数：单位名称、Buff 数据。</summary>
    public event Action<string, BuffEventData>? UnitBuffAdded;

    /// <summary>单位移除 Buff 事件。参数：单位名称、Buff 数据。</summary>
    public event Action<string, BuffEventData>? UnitBuffRemoved;
#pragma warning restore CS0067

    #region Room Management

    /// <summary>
    /// 创建房间。
    /// </summary>
    /// <param name="roomId">房间唯一 ID。</param>
    /// <returns>新建的房间。</returns>
    public GameRoom CreateRoom(string roomId) => _roomManager.CreateRoom(roomId);

    /// <summary>
    /// 按 ID 获取房间。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>对应的房间；不存在时返回 null。</returns>
    public GameRoom? GetRoom(string roomId) => _roomManager.GetRoom(roomId);

    /// <summary>
    /// 移除房间。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>移除成功返回 true；房间不存在返回 false。</returns>
    public bool RemoveRoom(string roomId) => _roomManager.RemoveRoom(roomId);

    /// <summary>获取全部房间。</summary>
    public IEnumerable<GameRoom> GetAllRooms() => _roomManager.GetAllRooms();

    /// <summary>
    /// 在指定房间创建 UnitModel 并加入对应阵营列表，返回 IUnitState 引用。
    /// 创建逻辑下沉到 Logic 层，避免外部模块直接实例化 Core.Models.UnitModel。
    /// </summary>
    public IUnitState CreateUnit(string roomId, string unitName, string camp) {
        var room = _roomManager.GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");
        var model = new UnitModel {
            UnitStateName = unitName,
            Camps = [camp],
        };
        if (camp == CampConstants.CampA)
            room.UnitsA.Add(model);
        else
            room.UnitsB.Add(model);

        // 触发 OnUnitCreated 事件（本地模式同步回调）
        OnUnitCreated?.Invoke(roomId, unitName, camp);
        return model;
    }

    #endregion

    #region Battle Flow

    /// <summary>
    /// 在指定房间开始战斗。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>房间对应的战斗管理器。</returns>
    /// <exception cref="InvalidOperationException">房间不存在时抛出。</exception>
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

    /// <summary>
    /// 结束指定战斗。
    /// </summary>
    /// <param name="battle">要结束的战斗管理器。</param>
    public static void EndBattle(BattleManager battle) {
        battle.EndBattle();
    }

    void IServerBattleService.EndBattle(BattleManager battle) {
        battle.EndBattle();
    }

    #endregion

    #region Skill

    /// <summary>
    /// 对目标施放技能（支持伤害、治疗、范围伤害、施加 Buff）。
    /// </summary>
    /// <param name="battle">战斗管理器（接口兼容保留参数，实际结算不依赖）。</param>
    /// <param name="caster">施法单位。</param>
    /// <param name="target">目标单位。</param>
    /// <param name="skill">技能模型。</param>
    /// <param name="allUnits">所有可命中的检测单位（范围伤害技能需要）。</param>
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

    /// <summary>
    /// 在所有房间中按名称查找单位模型。
    /// </summary>
    /// <param name="unitName">单位名称。</param>
    /// <returns>匹配的单位；未找到返回 null。</returns>
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

    /// <summary>
    /// 按帧推进单位集合的 Buff 状态。
    /// </summary>
    /// <param name="battle">战斗管理器（接口兼容保留参数，实际结算不依赖）。</param>
    /// <param name="units">要更新的单位集合。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
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

    /// <summary>
    /// 判断战斗是否已结束（任一阵营无存活单位）。
    /// </summary>
    /// <param name="room">房间数据。</param>
    /// <returns>已结束返回 true。</returns>
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
