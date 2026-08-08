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

    /// <summary>技能解析器（服务端注入）：按技能配置 ID 构造对应技能模型。空委托时读条/技能不可用。</summary>
    private Func<ushort, Core.Models.SkillModel?>? _skillResolver;

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
        room.Units.Add(model);

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
        unit.Position = new System.Numerics.Vector3(
            pos.X + dir.X * unit.MoveSpeed * deltaTime,
            0f,
            pos.Z + dir.Y * unit.MoveSpeed * deltaTime);
        unit.LookAtDir = new System.Numerics.Vector3(dir.X, 0f, dir.Y);
    }

    #endregion

    #region Skill

    /// <summary>
    /// 注入技能解析器（由服务端在房间初始化时调用，服务端持有配置表）。
    /// </summary>
    /// <param name="resolver">按技能配置 ID 构造技能模型的委托。</param>
    public void SetSkillResolver(Func<ushort, Core.Models.SkillModel?> resolver) {
        _skillResolver = resolver;
    }

    /// <summary>
    /// 服务端发起读条施法：先校验冷却（GCD + 个体技能冷却），通过后暂存技能与目标等待读条推进。
    /// 读条时长为技能配置的 SkillSpellTime。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="casterName">施法单位名称。</param>
    /// <param name="skillId">技能配置 ID。</param>
    /// <param name="targetName">目标单位名称（范围伤害技能传 null）。</param>
    /// <param name="targetPos">位置目标（范围伤害技能使用）。</param>
    /// <returns>冷却校验通过并成功发起读条返回 true。</returns>
    public bool BeginSpell(string roomId, string casterName, ushort skillId, string? targetName,
        System.Numerics.Vector3? targetPos = null) {
        var caster = FindUnitModel(roomId, casterName);
        if (caster is not Core.Models.UnitModel casterModel)
            return false;

        // 冷却校验：GCD 或该技能个体冷却中则拒绝
        if (casterModel.IsSkillCooling(skillId))
            return false;

        float castTime = _skillResolver?.Invoke(skillId)?.SkillSpellTime ?? 0f;
        casterModel.SpellingSkillId = skillId;
        casterModel.SpellRemaining = castTime;
        casterModel.SpellTargetName = targetName;
        casterModel.SpellTargetPos = targetPos;
        return true;
    }

    /// <summary>
    /// 按帧推进房间内所有单位的读条：剩余时间递减，读条归零时结算技能。
    /// 返回本帧完成读条并结算的单位（供服务端回写施法状态）。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    /// <returns>本帧读条完成的施法单位名称集合。</returns>
    public IReadOnlyList<string> TickSpells(string roomId, float deltaTime) {
        var room = _roomManager.GetRoom(roomId);
        if (room == null)
            return [];

        var finished = new List<string>();
        foreach (var unit in room.Units) {
            if (unit is not Core.Models.UnitModel model)
                continue;
            if (model.SpellingSkillId == 0)
                continue;

            model.SpellRemaining -= deltaTime;
            if (model.SpellRemaining <= 0f) {
                ResolveSpell(roomId, model);
                finished.Add(model.UnitStateName);
            }
        }
        return finished;
    }

    /// <summary>
    /// 按帧推进房间内所有单位的冷却（GCD + 个体技能冷却），不驱动施法状态机。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void TickCooldowns(string roomId, float deltaTime) {
        var room = _roomManager.GetRoom(roomId);
        if (room == null)
            return;

        foreach (var unit in room.Units) {
            if (unit is Core.Models.UnitModel model)
                model.ServerTickCooldowns(deltaTime);
        }
    }

    /// <summary>
    /// 查询单位指定技能的剩余个体冷却秒数（服务端回写 Pawn 用）。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitName">单位名称。</param>
    /// <param name="skillId">技能配置 ID。</param>
    /// <returns>剩余冷却秒数；单位不存在或无冷却返回 0。</returns>
    public float GetSkillCooldownRemaining(string roomId, string unitName, ushort skillId) {
        var unit = FindUnitModel(roomId, unitName);
        return unit is Core.Models.UnitModel model ? model.GetSkillCooldownRemaining(skillId) : 0f;
    }

    /// <summary>
    /// 读条完成结算：按暂存的技能 ID 构造技能模型并对目标结算，随后清空施法状态。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="casterModel">施法单位模型。</param>
    private void ResolveSpell(string roomId, Core.Models.UnitModel casterModel) {
        var skillId = casterModel.SpellingSkillId;
        var targetName = casterModel.SpellTargetName;
        var targetPos = casterModel.SpellTargetPos;

        // 清空施法状态（无论结算是否成功）
        casterModel.SpellingSkillId = 0;
        casterModel.SpellRemaining = 0f;
        casterModel.SpellTargetName = null;
        casterModel.SpellTargetPos = null;

        var skill = _skillResolver?.Invoke(skillId);
        if (skill == null)
            return;

        // 读条完成：写入个体技能冷却与全局冷却
        casterModel.ServerSetSkillCooldown(skillId, skill.SkillCooldownTime);
        casterModel.GcdTime = Math.Max(casterModel.GcdTime, skill.GCDTime);

        if (skill is SkillRangeDamageModel rangeSkill) {
            if (targetPos.HasValue)
                rangeSkill.SetTargetPosition(targetPos.Value);
            var allUnits = _roomManager.GetRoom(roomId)?.Units
                .Cast<Core.Interfaces.IUnitState>().ToList();
            if (allUnits != null)
                BattleResolver.ApplySkillRangeDamage(casterModel, allUnits, rangeSkill);
            return;
        }

        if (targetName == null)
            return;
        var target = FindUnitModel(roomId, targetName);
        if (target == null)
            return;
        CastSkill(casterModel, target, skill);
    }

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
        return room.Units.FirstOrDefault(u => u.UnitStateName == unitName);
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
        bool aAlive = BattleResolver.HasAliveUnits(
            room.Units.Where(u => u.Camps.Contains(CampConstants.CampA)));
        bool bAlive = BattleResolver.HasAliveUnits(
            room.Units.Where(u => u.Camps.Contains(CampConstants.CampB)));
        return !aAlive || !bAlive;
    }

    #endregion
}
