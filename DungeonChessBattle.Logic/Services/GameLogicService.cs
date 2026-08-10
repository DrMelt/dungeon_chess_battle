using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// Logic 层单房间门面服务，实现 IServerBattleService 供服务端使用。
/// 每个战斗房间持有独立实例（由服务端 BattleRoomServer 创建），直接持有本房间的
/// GameRoom、战斗流程与单位状态列表，不再经房间 ID 检索。
/// 战斗流程对外仅暴露 <see cref="IBattle"/> 抽象接口；技能结算/Buff 推进不携带战斗管理器参数。
/// </summary>
/// <param name="roomId">本服务所服务的房间唯一 ID。</param>
/// <param name="logger">日志器。</param>
public class GameLogicService(string roomId, ILogger<GameLogicService> logger) : IServerBattleService {
    private readonly ILogger<GameLogicService> _logger = logger;

    /// <summary>本房间数据（Logic 权威持有战斗字段所有权）。</summary>
    private readonly GameRoom _room = new(roomId);

    /// <summary>本房间的战斗单位状态列表（Logic 权威）。</summary>
    private readonly List<UnitModel> _units = [];

    /// <summary>本房间的战斗流程实例（首次启动战斗时创建）。</summary>
    private BattleManager? _battle;

    /// <summary>技能解析器（服务端注入）：按技能配置 ID 构造对应技能模型。空委托时读条/技能不可用。</summary>
    private Func<ushort, SkillModel?>? _skillResolver;

    /// <summary>单位创建事件（本房间）。参数：单位名称、阵营(字符串)。</summary>
    public event Action<string, string>? OnUnitCreated;

    /// <summary>战斗阶段变化事件（本房间）。参数：战斗阶段。</summary>
    public event Action<BattlePhase>? BattlePhaseChanged;

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

    #region Room Context

    /// <summary>本房间数据。</summary>
    public GameRoom Room => _room;

    /// <summary>本房间的战斗单位状态列表（Logic 权威）。</summary>
    public List<UnitModel> GetUnits() => _units;

    /// <summary>
    /// 在本房间创建 UnitModel 并加入单位列表，返回 IUnitState 引用。
    /// 创建逻辑下沉到 Logic 层，避免外部模块直接实例化 Core.Models.UnitModel。
    /// </summary>
    /// <param name="unitName">单位名称。</param>
    /// <param name="camp">阵营。</param>
    public IUnitState CreateUnit(string unitName, string camp) {
        var model = new UnitModel {
            UnitStateName = unitName,
            Camps = [camp],
        };
        _units.Add(model);

        // 触发 OnUnitCreated 事件
        OnUnitCreated?.Invoke(unitName, camp);
        return model;
    }

    /// <summary>
    /// 释放本房间的战斗上下文：关闭活跃标记并结束战斗。
    /// 由房间服务器在销毁时调用。
    /// </summary>
    public void Dispose() {
        _room.IsActive = false;
        _battle?.EndBattle();
    }

    #endregion

    #region Battle Flow

    /// <summary>
    /// 开始本房间战斗。
    /// </summary>
    /// <returns>本房间的战斗流程。</returns>
    public IBattle StartBattle() {
        var battle = _battle ??= new BattleManager();
        battle.StartBattle();

        // 触发 BattlePhaseChanged 事件
        BattlePhaseChanged?.Invoke(BattlePhase.Running);
        return battle;
    }

    /// <summary>
    /// 获取本房间的战斗流程实例。
    /// </summary>
    /// <returns>对应的战斗流程；不存在时返回 null。</returns>
    public IBattle? GetBattle() => _battle;

    /// <summary>
    /// 按帧推进本房间的战斗逻辑。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void TickBattle(float deltaTime) {
        _battle?.Tick(deltaTime);
    }

    /// <summary>
    /// 结束本房间指定战斗。
    /// </summary>
    /// <param name="battle">要结束的战斗流程。</param>
    public void EndBattle(IBattle battle) {
        battle.EndBattle();
    }

    #endregion

    #region Skill

    /// <summary>
    /// 注入技能解析器（由服务端在房间初始化时调用，服务端持有配置表）。
    /// </summary>
    /// <param name="resolver">按技能配置 ID 构造技能模型的委托。</param>
    public void SetSkillResolver(Func<ushort, SkillModel?> resolver) {
        _skillResolver = resolver;
    }

    /// <summary>
    /// 服务端发起读条施法：先校验冷却（GCD + 个体技能冷却），通过后暂存技能与目标等待读条推进。
    /// 读条时长为技能配置的 SkillSpellTime。
    /// </summary>
    /// <param name="casterName">施法单位名称。</param>
    /// <param name="skillId">技能配置 ID。</param>
    /// <param name="targetName">目标单位名称（范围伤害技能传 null）。</param>
    /// <param name="targetPos">位置目标（范围伤害技能使用）。</param>
    /// <returns>冷却校验通过并成功发起读条返回 true。</returns>
    public bool BeginSpell(string casterName, ushort skillId, string? targetName,
        System.Numerics.Vector3? targetPos = null) {
        var caster = FindUnitModel(casterName);
        if (caster is not UnitModel casterModel)
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
    /// 按帧推进本房间所有单位的读条：剩余时间递减，读条归零时结算技能。
    /// 返回本帧完成读条并结算的单位（供服务端回写施法状态）。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    /// <returns>本帧读条完成的施法单位名称集合。</returns>
    public IReadOnlyList<string> TickSpells(float deltaTime) {
        var finished = new List<string>();
        foreach (var unit in _units) {
            if (unit is not UnitModel model)
                continue;
            if (model.SpellingSkillId == 0)
                continue;

            model.SpellRemaining -= deltaTime;
            if (model.SpellRemaining <= 0f) {
                ResolveSpell(model);
                finished.Add(model.UnitStateName);
            }
        }
        return finished;
    }

    /// <summary>
    /// 按帧推进本房间所有单位的冷却（GCD + 个体技能冷却），不驱动施法状态机。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void TickCooldowns(float deltaTime) {
        foreach (var unit in _units) {
            if (unit is UnitModel model)
                model.ServerTickCooldowns(deltaTime);
        }
    }

    /// <summary>
    /// 查询单位指定技能的剩余个体冷却秒数（服务端回写 Pawn 用）。
    /// </summary>
    /// <param name="unitName">单位名称。</param>
    /// <param name="skillId">技能配置 ID。</param>
    /// <returns>剩余冷却秒数；单位不存在或无冷却返回 0。</returns>
    public float GetSkillCooldownRemaining(string unitName, ushort skillId) {
        var unit = FindUnitModel(unitName);
        return unit is UnitModel model ? model.GetSkillCooldownRemaining(skillId) : 0f;
    }

    /// <summary>
    /// 读条完成结算：按暂存的技能 ID 构造技能模型并对目标结算，随后清空施法状态。
    /// </summary>
    /// <param name="casterModel">施法单位模型。</param>
    private void ResolveSpell(UnitModel casterModel) {
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
            var allUnits = _units.Cast<IUnitState>().ToList();
            BattleResolver.ApplySkillRangeDamage(casterModel, allUnits, rangeSkill);
            return;
        }

        if (targetName == null)
            return;
        var target = FindUnitModel(targetName);
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
    /// 在本房间中按名称查找单位模型。
    /// </summary>
    /// <param name="unitName">单位名称。</param>
    /// <returns>匹配的单位；未找到返回 null。</returns>
    public IUnitState? FindUnitModel(string unitName)
        => _units.FirstOrDefault(u => u.UnitStateName == unitName);

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
    /// 判断本房间战斗是否已结束（任一阵营无存活单位）。
    /// </summary>
    /// <returns>已结束返回 true。</returns>
    public bool CheckBattleEnded() {
        bool aAlive = BattleResolver.HasAliveUnits(
            _units.Where(u => u.Camps.Contains(CampConstants.CampA)));
        bool bAlive = BattleResolver.HasAliveUnits(
            _units.Where(u => u.Camps.Contains(CampConstants.CampB)));
        return !aAlive || !bAlive;
    }

    #endregion
}
