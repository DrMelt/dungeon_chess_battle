using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Rooms;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// Logic 层对外门面服务，实现 IServerBattleService 供服务端使用。
/// 组合 RoomManager 和 BattleResolver，提供房间管理、战斗流程、技能结算等全部业务操作。
/// 战斗流程对外仅暴露 <see cref="IBattle"/> 抽象接口；技能结算/Buff 推进不携带战斗管理器参数。
/// </summary>
public class GameLogicService : IServerBattleService {
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

        // 触发 OnUnitCreated 事件
        OnUnitCreated?.Invoke(roomId, unitName, camp);
        return model;
    }

    #endregion

    #region Battle Flow

    /// <summary>
    /// 在指定房间开始战斗。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>房间对应的战斗流程。</returns>
    /// <exception cref="InvalidOperationException">房间不存在时抛出。</exception>
    public IBattle StartBattleInRoom(string roomId) {
        _ = GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");

        var battle = _roomManager.GetOrCreateBattle(roomId);
        battle.StartBattle();

        // 触发 BattlePhaseChanged 事件
        BattlePhaseChanged?.Invoke(roomId, BattlePhase.Running);
        return battle;
    }

    /// <summary>
    /// 按房间 ID 获取战斗流程实例。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>对应的战斗流程；不存在时返回 null。</returns>
    public IBattle? GetBattle(string roomId) => _roomManager.GetBattle(roomId);

    /// <summary>
    /// 按帧推进指定房间的战斗逻辑。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void TickBattle(string roomId, float deltaTime) {
        _roomManager.GetBattle(roomId)?.Tick(deltaTime);
    }

    /// <summary>
    /// 结束指定战斗。
    /// </summary>
    /// <param name="battle">要结束的战斗流程。</param>
    public void EndBattle(IBattle battle) {
        battle.EndBattle();
    }

    #endregion

    #region Movement

    /// <summary>
    /// 按移动方向推进指定单位位置（XZ 平面，Y 恒为 0），并随移动方向更新朝向。
    /// 移动方向超长时归一化。移动/速度规则统一在此层结算，Entities/Server 只做转发。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitName">单位名称。</param>
    /// <param name="moveDir">移动方向向量（无需单位化）。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void UpdatePlayerMovement(string roomId, string unitName, System.Numerics.Vector2 moveDir, float deltaTime) {
        if (moveDir == System.Numerics.Vector2.Zero || deltaTime <= 0f)
            return;

        var unit = FindUnitModel(roomId, unitName);
        if (unit == null)
            return;

        var dir = System.Numerics.Vector2.Normalize(moveDir);
        var pos = unit.Position;
        unit.SetPosition(new System.Numerics.Vector3(
            pos.X + dir.X * unit.MoveSpeed * deltaTime,
            0f,
            pos.Z + dir.Y * unit.MoveSpeed * deltaTime));
        unit.LookAtDir = new System.Numerics.Vector3(dir.X, 0f, dir.Y);
    }

    #endregion

    #region Skill

    /// <summary>
    /// 对目标施放技能（支持伤害、治疗、范围伤害、施加 Buff）。
    /// </summary>
    /// <param name="caster">施法单位。</param>
    /// <param name="target">目标单位。</param>
    /// <param name="skill">技能模型。</param>
    /// <param name="allUnits">所有可命中的检测单位（范围伤害技能需要）。</param>
    public void CastSkill(IUnitState caster, IUnitState target, SkillModel skill,
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

    /// <summary>
    /// 在指定房间中按名称查找单位模型。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitName">单位名称。</param>
    /// <returns>匹配的单位；未找到返回 null。</returns>
    public IUnitState? FindUnitModel(string roomId, string unitName) {
        var room = _roomManager.GetRoom(roomId);
        if (room == null)
            return null;
        return room.UnitsA.Concat(room.UnitsB)
            .FirstOrDefault(u => u.UnitStateName == unitName);
    }

    #endregion

    #region Buffs & Status

    /// <summary>
    /// 按帧推进单位集合的 Buff 状态。
    /// </summary>
    /// <param name="units">要更新的单位集合。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void UpdateBuffs(IEnumerable<IUnitState> units, double deltaTime) {
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

    #endregion
}
