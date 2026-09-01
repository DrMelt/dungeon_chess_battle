using System.Numerics;
using DungeonChessBattle.Battle.Shared;
using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Shared.Intelligence;
using DungeonChessBattle.Battle.Shared.Movement;
using DungeonChessBattle.Battle.Logic.Buffs;
using DungeonChessBattle.Battle.Logic.Combat;
using DungeonChessBattle.Battle.Logic.Events;
using DungeonChessBattle.Battle.Logic.Hates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Logic;

/// <summary>
/// 战斗世界实现：自持单位权威状态，统一驱动移动、读条、冷却、Buff、仇恨与技能结算。
/// 面向 <see cref="BattleUnit"/> 领域实体读写，不依赖网络载体与配置仓库。
/// <see cref="ApplyDecisions"/> 先触发 AI 决策产出意图，<see cref="Tick"/> 消费意图并推进战斗、返回帧事件流；
/// 事件流是仇恨推衍的唯一真相源。阶段由宿主写 <c>CurrentPhase</c>，死亡不产出事件而由生命值派生。
/// 宿主提交意图见 <see cref="BattleIntentHub"/>。
/// </summary>
/// <param name="relations">副本配置的阵营关系函数，由房间按副本装配。</param>
/// <param name="movementScene">竞技场移动场景，由房间按副本布局构建，与战斗世界同生命周期。</param>
/// <param name="hateSettings">仇恨系统参数，可选覆盖。</param>
/// <param name="logger">AI 决策日志，可选注入。</param>
public sealed partial class BattleScene(
    CampRelationResolver relations,
    IMovementScene movementScene,
    HateSettings? hateSettings = null,
    ILogger<BattleScene>? logger = null) : IBattleSceneView {
    /// <summary>副本配置的阵营关系函数，敌我判定的唯一来源。</summary>
    private readonly CampRelationResolver _relations = relations;

    /// <summary>仇恨系统参数，未注入时用默认。</summary>
    private readonly HateSettings _hateSettings = hateSettings ?? new HateSettings();

    /// <summary>AI 决策与应用日志，未注入时用 NullLogger 静默。</summary>
    private readonly ILogger<BattleScene> _logger = logger ?? NullLogger<BattleScene>.Instance;

    /// <summary>竞技场移动场景：静态障碍与单位互斥的空间载体，构造后只读，与战斗世界同生命周期。</summary>
    private readonly IMovementScene _movementScene = movementScene;

    /// <summary>每帧战斗事件日志：处理开始清空，处理中只增追加，帧末经只读视图消费与外送。</summary>
    private readonly BattleEventLog _eventLog = new();

    /// <summary>单位 ID → 领域单位索引。</summary>
    private readonly Dictionary<UnitId, BattleUnit> _unitById = [];

    /// <summary>全部战斗单位，按注册顺序。</summary>
    private readonly List<BattleUnit> _units = [];

    /// <inheritdoc />
    public IReadOnlyList<IBattleUnitView> Units => _units;

    /// <summary>全部战斗单位的写面枚举，供宿主装配与状态同步遍历；只读消费走 <see cref="Units"/>。</summary>
    public IReadOnlyList<BattleUnit> BattleUnits => _units;

    /// <inheritdoc />
    public IBattleUnitView? FindUnit(ushort netId) =>
        _unitById.TryGetValue(netId, out var unit) ? unit : null;

    /// <summary>按网络 ID 查领域单位写面，供输入门面解析意图与宿主增删实体用；只读消费走 <see cref="FindUnit"/>。</summary>
    public BattleUnit? FindBattleUnit(ushort netId) =>
        _unitById.TryGetValue(netId, out var unit) ? unit : null;

    /// <summary>战斗开始 Unix 秒，取战斗世界构造时刻；无开战重置点，准备期耗时计入其中。</summary>
    public long BattleStartUnixTime {
        get; private set;
    } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>战斗已运行的秒数，Running 期间累加。</summary>
    public float ElapsedTime {
        get; private set;
    }

    /// <summary>战斗阶段：宿主可写——服务端开战、回放重跑、在线端跟随房间权威。</summary>
    public BattlePhase CurrentPhase { get; set; } = BattlePhase.Waiting;

    /// <summary>战斗是否已结束，由阶段推导。</summary>
    public bool IsFinished => CurrentPhase == BattlePhase.Finished;

    /// <summary>Buff 全局结算间隔，秒；所有存活 Buff 在同一节拍点同时结算。</summary>
    private const double BuffTickInterval = 3.0;

    /// <summary>距下一次 Buff 全局结算的剩余时间，秒。</summary>
    private double _buffTickRemaining = BuffTickInterval;

    /// <summary>注册一个战斗单位到战斗世界。</summary>
    public void AddUnit(BattleUnit unit) {
        ArgumentNullException.ThrowIfNull(unit);
        if (!_units.Contains(unit)) {
            _units.Add(unit);
            _unitById[unit.UnitId] = unit;
        }
    }

    /// <summary>移除已注册的战斗单位，并清空其仇恨条目。</summary>
    public void RemoveUnit(BattleUnit unit) {
        _units.Remove(unit);
        _unitById.Remove(unit.UnitId);
        ClearHateEntries(unit);
    }

    /// <summary>清空单位自身仇恨表，并从其他所有单位表中移除其条目；死亡或移除时调用。</summary>
    private void ClearHateEntries(BattleUnit deadUnit) {
        deadUnit.RuntimeState.Hates.Clear();
        foreach (var unit in _units) {
            if (unit != deadUnit)
                unit.RuntimeState.Hates.RemoveTarget(deadUnit.UnitId);
        }
    }

    /// <summary>
    /// 死者仇恨账本清理：每帧对全部死者执行，故置于仇恨推衍之后，避免死者因自身伤害事件被重写。
    /// 死亡不产出事件，一切死亡后续处理依 IsDead 派生。
    /// </summary>
    private void CleanupDeaths() {
        foreach (var unit in _units.ToArray())
            if (unit.IsDead)
                ClearHateEntries(unit);
    }

    /// <summary>
    /// 维持「聚焦目标必存活」不变式：聚焦指向不存在或已死亡单位时清除。
    /// 与 <see cref="SubmitFocus"/> 的设置期校验同源，死亡不产出事件，故随本帧生命值结果收敛。
    /// </summary>
    private void ClearDeadFocusTargets() {
        foreach (var unit in _units) {
            if (unit.FocusTarget.IsDefault || FindBattleUnit(unit.FocusTarget) is { IsDead: false })
                continue;
            unit.FocusTarget = UnitId.None;
        }
    }

    /// <summary>
    /// 提交移动意图：写入该单位本帧移动输入，单位不存在即丢弃。宿主与回放一律经 <see cref="BattleIntentHub.Submit"/> 转入。
    /// </summary>
    /// <returns>单位存在并已写入意图返回 true。</returns>
    internal bool SubmitMove(ushort netId, Vector2 moveDirection) {
        if (!_unitById.TryGetValue(netId, out var unit))
            return false;
        unit.MoveInput = moveDirection;
        return true;
    }

    /// <summary>
    /// 提交聚焦目标：写该单位的持续展示态，0 表示清除。目标必须存在且存活，允许目标为自己；
    /// 提交者或目标不合法即不接管。
    /// </summary>
    /// <returns>已写入返回 true。</returns>
    internal bool SubmitFocus(ushort netId, ushort targetNetId) {
        if (!_unitById.TryGetValue(netId, out var unit))
            return false;
        if (targetNetId == 0) {
            unit.FocusTarget = UnitId.None;
            return true;
        }
        if (!_unitById.TryGetValue(targetNetId, out var target) || target.IsDead)
            return false;
        unit.FocusTarget = target.UnitId;
        return true;
    }

    /// <summary>
    /// 施法裁定：技能属该单位且 <see cref="SkillCastValidator.CanCast"/> 通过后，瞬发立即结算、
    /// 否则写入读条状态与目标，事件直写本帧日志。未通过只记日志不改状态，意图不退回——重投由输入源负责。
    /// </summary>
    private void AttemptCast(BattleUnit caster, SkillKeyId skillKey, BattleUnit? target, Vector2? targetPos,
        BattleEventLog log) {
        string targetName = target?.UnitName ?? "(position)";
        var skill = caster.GetSkill(skillKey);
        if (skill == null) {
            LogSkillNotFound(caster.UnitName, skillKey.Id);
            return;
        }

        if (!SkillCastValidator.CanCast(caster, skill, target, targetPos, _relations)) {
            LogCastRejected(caster.UnitName, skillKey.Id, targetName);
            return;
        }

        // 瞬发技能：校验通过即立即结算，不进入读条状态机，无读条可被打断
        if (skill.SpellTime <= 0f) {
            ResolveCast(caster, skill, target, targetPos, log);
            return;
        }

        caster.SkillCasting = skillKey;
        caster.SkillCastRemaining = skill.SpellTime;
        caster.RuntimeState.CastTarget = target;
        caster.RuntimeState.CastTargetPos = targetPos;
        log.Append(new CastStarted(caster.UnitId, skillKey, target?.UnitId));
    }

    /// <summary>
    /// 取消单位当前读条施法：产生 CastCanceled 事件并清理读条状态；无读条为空操作。
    /// 唯一触发路径是读条推进段的"本帧有位移意图"判定。
    /// </summary>
    private static void CancelCast(BattleUnit unit, BattleEventLog log) {
        if (unit.SkillCasting == default)
            return;
        log.Append(new CastCanceled(unit.UnitId, unit.SkillCasting));
        unit.SkillCasting = default;
        unit.SkillCastRemaining = 0f;
        unit.RuntimeState.ClearCast();
    }

    /// <summary>
    /// AI 前置推进：逐单位触发自治决策，产出的移动与施法意图直写单位字段，不触碰结算状态。
    /// 须在 <see cref="Tick"/> 之前由 <see cref="BattleIntentHub.PrepareTick"/> 调用。
    /// </summary>
    internal void ApplyDecisions() {
        if (CurrentPhase != BattlePhase.Running)
            return;

        foreach (var unit in _units) {
            if (unit.IsDead || unit.Intelligence is not { } intelligence)
                continue;

            // 正在读条：本帧不投移动意图，原地等读条完成，避免移动打断自身读条
            if (unit.SkillCasting != default)
                continue;

            var decision = intelligence.Decide(unit, this, _relations);
            switch (decision.Kind) {
                case EnemyDecisionKind.MoveTo:
                    unit.MoveInput = decision.MoveDirection;
                    break;

                case EnemyDecisionKind.CastSkill:
                    SubmitAiCast(unit, decision.SkillId, decision.TargetNetId, decision.TargetPosition);
                    break;

                    // Idle 与未知决策不投意图：静止是缺省结果
            }
        }
    }

    /// <summary>
    /// AI 决策的施法意图投递：按技能目标类型解析单位目标后写入该单位的 <c>CastInput</c>，位置锚点恒随决策携带；
    /// 目标解不到即本帧不投，下一帧重新决策。
    /// </summary>
    private void SubmitAiCast(BattleUnit caster, SkillKeyId skillKey, UnitId targetNetId, Vector2 targetPosition) {
        BattleUnit? target = null;
        if (caster.GetSkill(skillKey) is { NeedUnitTarget: true }) {
            if (!_unitById.TryGetValue(targetNetId, out var targetUnit))
                return;
            target = targetUnit;
        }

        caster.CastInput = new CastIntent(skillKey, target, targetPosition);
    }

    /// <summary>
    /// 按帧推进位移解算、施法裁定与读条、冷却与 Buff，返回本帧领域事件；仅在 Running 阶段推进，结束条件满足时切 Finished。
    /// 单出口：两类意图在末尾统一作废，静止与无待决施法是缺省结果。作废点必须晚于读条推进段——它是移动意图的最后一个读者。
    /// </summary>
    public IReadOnlyList<IBattleEvent> Tick(float deltaTime) {
        _eventLog.Clear();

        if (CurrentPhase == BattlePhase.Running) {
            ElapsedTime += deltaTime;

            // 全局 Buff 节拍：每满一个间隔所有 Buff 同时结算一跳
            _buffTickRemaining -= deltaTime;
            int buffJumps = 0;
            while (_buffTickRemaining <= 0) {
                _buffTickRemaining += BuffTickInterval;
                buffJumps++;
            }

            // 位移解算在前：其后的施法裁定与结算一律读本帧新位置
            ResolveMovement(deltaTime);

            foreach (var unit in _units.ToArray()) {
                TickCasting(unit, deltaTime, _eventLog);
            }

            foreach (var unit in _units.ToArray()) {
                TickCooldowns(unit, deltaTime);
            }

            foreach (var unit in _units.ToArray()) {
                TickBuffs(unit, deltaTime, _eventLog, buffJumps);
            }

            TryEndBattle();

            // 事件流单一消费点：先按单位自身仇恨规则求效果并落账；落账路由到持有者仇恨表
            foreach (var effect in HateDispatcher.Dispatch(_eventLog, _units, _unitById.GetValueOrDefault, _hateSettings, _relations)) {
                if (_unitById.TryGetValue(effect.HolderNetId, out var holder))
                    holder.RuntimeState.Hates.ApplyEffect(effect);
            }

            CleanupDeaths();

            // 聚焦目标必存活：死亡当帧即清零
            ClearDeadFocusTargets();
        }

        // 本帧意图统一作废，下一帧由输入源重投
        foreach (var unit in _units) {
            unit.MoveInput = Vector2.Zero;
            unit.CastInput = null;
        }

        return _eventLog;
    }

    /// <summary>
    /// 位移解算：本帧移动意图本帧生效，服务端、在线与回放同源同序；只读不清理，作废在 <see cref="Tick"/> 末。
    /// 静止与死亡单位不入意图集，既不被推开也不构成他人障碍；遍历按注册顺序，保证互斥让位的解算顺序三端一致。
    /// </summary>
    private void ResolveMovement(float dt) {
        var intents = new List<MoveIntent>(_units.Count);
        foreach (var unit in _units) {
            if (unit.IsDead || unit.MoveInput.LengthSquared() <= 0.0001f || unit.BaseSpeed <= 0f)
                continue;
            intents.Add(new MoveIntent(unit.UnitId, unit.Position,
                Vector2.Normalize(unit.MoveInput), unit.BaseSpeed, unit.BodyRadius));
        }

        var results = _movementScene.Resolve(intents, dt);
        for (var i = 0; i < intents.Count; i++) {
            if (!_unitById.TryGetValue(intents[i].ActorId, out var unit))
                continue;
            unit.Position = results[i];
            if (unit.Direction != intents[i].Direction)
                unit.Direction = intents[i].Direction;
        }
    }

    /// <summary>
    /// 判定战斗是否结束：任一阵营无存活单位则结束；满足条件时切换 Finished，每场仅执行一次。
    /// </summary>
    private void TryEndBattle() {
        if (CurrentPhase == BattlePhase.Finished)
            return;

        var allCamps = _units.SelectMany(u => u.Camps).Distinct().ToHashSet();
        if (allCamps.Count < 2)
            return;

        var aliveCamps = _units.Where(u => !u.IsDead).SelectMany(u => u.Camps).Distinct().ToHashSet();
        if (aliveCamps.Count >= allCamps.Count)
            return;

        CurrentPhase = BattlePhase.Finished;
    }

    /// <summary>
    /// 施法裁定与读条推进，逐单位三步：消费本帧施法意图 → 本帧有非零位移意图且在读条则取消 → 推进读条，扣完即结算。
    /// 消费排在打断判定之前，故同 tick 内「起读条 + 位移」当帧即被打断；三步都在位移解算之后，射程判定与结算读同一份本帧位置。
    /// </summary>
    private void TickCasting(BattleUnit unit, float deltaTime, BattleEventLog log) {
        // 意图不在此清理，作废收在 Tick 末
        if (unit.CastInput is { } cast)
            AttemptCast(unit, cast.Skill, cast.Target, cast.TargetPos, log);

        if (unit.SkillCasting != default && unit.MoveInput.LengthSquared() > 0.0001f)
            CancelCast(unit, log);

        if (unit.SkillCasting == default)
            return;

        unit.SkillCastRemaining -= deltaTime;
        if (unit.SkillCastRemaining > 0f)
            return;

        var skill = unit.GetSkill(unit.SkillCasting);
        var state = unit.RuntimeState;
        if (skill != null)
            ResolveCast(unit, skill, state.CastTarget, state.CastTargetPos, log);
        unit.SkillCasting = default;
        unit.SkillCastRemaining = 0f;
        unit.RuntimeState.ClearCast();
    }

    /// <summary>
    /// 推进个体冷却与全局冷却：剩余秒数递减，个体冷却到期移除条目，全局冷却到期归零。
    /// 二者一律只存剩余秒，截止 tick 由同步通道折算。
    /// </summary>
    private static void TickCooldowns(BattleUnit unit, double deltaTime) {
        float dt = (float)deltaTime;
        if (unit.GcdRemaining > 0f)
            unit.GcdRemaining = MathF.Max(0f, unit.GcdRemaining - dt);

        var entries = unit.RuntimeState.Cooldowns;
        if (entries.Count == 0)
            return;
        for (int i = entries.Count - 1; i >= 0; i--) {
            CooldownEntry entry = entries[i];
            float remaining = entry.Remaining - dt;
            if (remaining <= 0f)
                entries.RemoveAt(i);
            else
                entry.Remaining = remaining;
        }
    }

    /// <summary>推进 Buff 全局节拍；结构变化时保留存活实例。</summary>
    private void TickBuffs(BattleUnit target, double deltaTime, BattleEventLog log, int buffJumps) {
        var list = target.RuntimeState.Buffs;
        if (list.Count == 0)
            return;

        double tickSeconds = buffJumps * BuffTickInterval;
        var snapshot = target.Snapshot;
        var alive = new List<ActiveBuff>(list.Count);
        foreach (var buff in list) {
            foreach (var e in BuffTickProcessor.Tick(buff.Definition, buff.Effect, buff.Instance, snapshot, deltaTime, tickSeconds)) {
                ApplyEventEffect(e);
                log.Append(e);
            }
            if (buff.Instance.IsAlive)
                alive.Add(buff);
        }

        if (alive.Count != list.Count) {
            list.Clear();
            list.AddRange(alive);
        }
    }

    /// <summary>读条完成与瞬发立即结算共用：写入权威个体冷却，推进全局冷却，并执行技能多态结算。</summary>
    private void ResolveCast(BattleUnit caster, SkillDefinition skill, BattleUnit? target, Vector2? targetPos, BattleEventLog log) {
        SetCooldownAuthoritative(caster, skill.SkillId, skill.CooldownTime);
        caster.GcdRemaining = MathF.Max(caster.GcdRemaining, skill.GcdTime);

        var resolution = skill.Effect.Resolve(new SkillResolveContext(skill, caster, target, targetPos, _units, _relations));
        foreach (var evt in resolution.Events) {
            ApplyEventEffect(evt);
            log.Append(evt);
        }
        foreach (var buff in resolution.Buffs)
            ApplyBuffToTarget(buff, log);

        log.Append(new CastCompleted(caster.UnitId, skill.SkillId, target?.UnitId));
    }

    /// <summary>写入单位的权威个体冷却，同技能已有冷却时刷新取较大值。</summary>
    private static void SetCooldownAuthoritative(BattleUnit unit, SkillKeyId skillKey, float remaining) {
        var entries = unit.RuntimeState.Cooldowns;
        foreach (var entry in entries) {
            if (entry.SkillKey != skillKey)
                continue;
            if (remaining <= entry.Remaining)
                return;
            entry.Remaining = remaining;
            return;
        }
        entries.Add(new CooldownEntry(skillKey, remaining));
    }

    /// <summary>领域事件副作用统一应用：伤害/治疗落到目标生命值，单位已移除则忽略；新副作用在此扩展。</summary>
    private void ApplyEventEffect(IBattleEvent evt) {
        if (evt is DamageOccurred dmg) {
            if (_unitById.TryGetValue(dmg.TargetNetId, out var unit))
                ApplyHealthDelta(unit, -dmg.AppliedDamage);
        }
        else if (evt is HealOccurred heal) {
            if (_unitById.TryGetValue(heal.TargetNetId, out var unit))
                ApplyHealthDelta(unit, heal.ActualHeal);
        }
    }

    /// <summary>施加 Buff 到目标：叠加或新建运行时实例，产出 BuffApplied 事件。</summary>
    private void ApplyBuffToTarget(BuffToApply buff, BattleEventLog log) {
        if (!_unitById.TryGetValue(buff.TargetNetId, out var target))
            return;

        var list = target.RuntimeState.Buffs;
        var existing = list.FirstOrDefault(b => b.Instance.BuffTypeId == buff.Definition.BuffTypeId);
        int stacks;
        if (existing != null) {
            existing.Instance.Remaining = Math.Max(existing.Instance.Remaining, buff.Definition.Duration);
            existing.Instance.Stacks = Math.Min(existing.Instance.Stacks + 1, buff.Definition.MaxStacks);
            stacks = existing.Instance.Stacks;
        }
        else {
            list.Add(new ActiveBuff(BuffService.CreateInstance(buff.Definition, target.UnitId, buff.From, buff.FromNetId),
                buff.Definition, buff.Definition.Effect));
            stacks = 1;
        }

        log.Append(new BuffApplied(target.UnitId, buff.Definition.BuffTypeId, stacks));
    }

    /// <summary>生命值增量修正，钳制到 [0, MaxHealth]。</summary>
    private static void ApplyHealthDelta(BattleUnit unit, float delta) {
        unit.Health = Math.Clamp(unit.Health + delta, 0f, unit.MaxHealth);
    }

    #region 日志

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "[BattleScene] {Caster} cannot find skill {SkillId}.")]
    private partial void LogSkillNotFound(string caster, string skillId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[BattleScene] {Caster} cast rejected: {SkillId} on {Target}.")]
    private partial void LogCastRejected(string caster, string skillId, string target);

    #endregion
}

