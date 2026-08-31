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
/// <see cref="ApplyDecisions"/> 先触发 AI 决策，<see cref="Tick"/> 结算移动并推进战斗、返回帧事件流；
/// 事件流是仇恨推衍的唯一真相源。阶段由宿主写 <c>CurrentPhase</c>，死亡不产出事件而由生命值派生。
/// </summary>
/// <param name="relations">副本配置的阵营关系函数，由房间按副本装配。</param>
/// <param name="movementScene">竞技场移动场景，由房间按副本布局构建注入，与战斗世界同生命周期。</param>
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

    /// <summary>Tick 之外产出的跨帧事件缓冲，下一帧 Tick 开头汇入帧日志统一外送。</summary>
    private readonly List<IBattleEvent> _pendingEvents = [];

    /// <summary>单位 ID → 领域单位索引。</summary>
    private readonly Dictionary<UnitId, BattleUnit> _unitById = [];

    /// <summary>全部战斗单位，按注册顺序。</summary>
    private readonly List<BattleUnit> _units = [];

    /// <inheritdoc />
    public IReadOnlyList<IBattleUnitView> Units => _units;

    /// <summary>全部战斗单位具体列表，回放端位移结算与外部装配用。</summary>
    public IReadOnlyList<BattleUnit> BattleUnits => _units;

    /// <inheritdoc />
    public IBattleUnitView? FindUnit(ushort netId) =>
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
    /// 提交移动输入：写入单位移动输入并按世界规则处理"移动即打断读条"。玩家输入与回放输入共用。
    /// </summary>
    public void SubmitMove(ushort netId, Vector2 moveDirection) {
        if (!_unitById.TryGetValue(netId, out var unit))
            return;
        SetMoveInput(unit, moveDirection);
    }

    /// <summary>
    /// 发起读条施法：技能存在、归属、状态与目标/位置校验通过后写入读条状态并暂存目标。
    /// </summary>
    /// <returns>校验通过并成功发起返回 true。</returns>
    public bool BeginCast(BattleUnit caster, SkillKeyId skillKey, BattleUnit? target, Vector2? targetPos) {
        var skill = caster.GetSkill(skillKey);
        if (skill == null)
            return false;
        if (!SkillCastValidator.CanCast(caster, skill, target, targetPos, _relations))
            return false;

        // 瞬发技能：校验通过即立即结算，不进入读条状态机，不受移动取消施法影响
        if (skill.SpellTime <= 0f) {
            var log = new BattleEventLog();
            ResolveCast(caster, skill, target, targetPos, log);
            _pendingEvents.AddRange(log);
            return true;
        }

        caster.SkillCasting = skillKey;
        caster.SkillCastRemaining = skill.SpellTime;
        caster.RuntimeState.CastTarget = target;
        caster.RuntimeState.CastTargetPos = targetPos;
        _pendingEvents.Add(new CastStarted(caster.UnitId, skillKey, target?.UnitId));
        return true;
    }

    /// <summary>
    /// 取消单位当前读条施法：产生 CastCanceled 事件并清理读条状态；无读条为空操作。
    /// </summary>
    public void CancelCast(BattleUnit unit) {
        if (unit.SkillCasting == default)
            return;
        _pendingEvents.Add(new CastCanceled(unit.UnitId, unit.SkillCasting));
        unit.SkillCasting = default;
        unit.SkillCastRemaining = 0f;
        unit.RuntimeState.ClearCast();
    }

    /// <summary>
    /// AI 前置推进：逐个触发敌方单位的自治决策，动作经本场景执行。
    /// 必须在移动结算之前调用，移动输入本帧生效。
    /// </summary>
    public void ApplyDecisions() {
        if (CurrentPhase != BattlePhase.Running)
            return;

        foreach (var unit in _units) {
            if (unit.IsDead || unit.Intelligence is not { } intelligence)
                continue;

            // 正在读条：原地等待读条完成，避免移动打断自身读条
            if (unit.SkillCasting != default) {
                SetMoveInput(unit, Vector2.Zero);
                continue;
            }

            var decision = intelligence.Decide(unit, this, _relations);
            switch (decision.Kind) {
                case EnemyDecisionKind.Idle:
                    SetMoveInput(unit, Vector2.Zero);
                    break;

                case EnemyDecisionKind.MoveTo:
                    SetMoveInput(unit, decision.MoveDirection);
                    break;

                case EnemyDecisionKind.CastSkill:
                    SetMoveInput(unit, Vector2.Zero);
                    RequestCast(unit, decision.SkillId, decision.TargetNetId, decision.TargetPosition);
                    break;

                default:
                    // 未知决策类型按静止退化，决策器为领域内可控代码，正常不产生
                    SetMoveInput(unit, Vector2.Zero);
                    break;
            }
        }
    }

    /// <summary>写入移动输入并处理"移动即打断读条"；零向量表示静止，不打断读条。</summary>
    private void SetMoveInput(BattleUnit unit, Vector2 moveDirection) {
        unit.MoveInput = moveDirection;
        if (moveDirection.LengthSquared() > 0.0001f)
            CancelCast(unit);
    }

    /// <summary>按技能目标类型解析单位目标后发起读条；目标丢失或校验失败仅记日志，下一帧重新决策。</summary>
    private void RequestCast(BattleUnit caster, SkillKeyId skillKey, UnitId targetNetId, Vector2 targetPosition) {
        var skill = caster.GetSkill(skillKey);
        if (skill == null) {
            LogSkillNotFound(caster.UnitName, skillKey.Id);
            return;
        }

        BattleUnit? target = null;
        if (skill.NeedUnitTarget) {
            if (!_unitById.TryGetValue(targetNetId, out var targetUnit))
                return;
            target = targetUnit;
        }

        string targetName = target?.UnitName ?? "(position)";
        if (!BeginCast(caster, skillKey, target, targetPosition)) {
            LogCastRejected(caster.UnitName, skillKey.Id, targetName);
            return;
        }

        LogCastStarted(caster.UnitName, skillKey.Id, targetName);
    }

    /// <summary>
    /// 按帧推进移动结算、读条、冷却与 Buff，返回本帧领域事件。
    /// 仅在 Running 阶段推进；战斗结束条件满足时切换 Finished。
    /// </summary>
    public IReadOnlyList<IBattleEvent> Tick(float deltaTime) {
        if (CurrentPhase != BattlePhase.Running) {
            // 非 Running 不推进不外送事件；跨帧缓冲一并清空，避免战斗结束后滞留。
            _pendingEvents.Clear();
            return [];
        }

        ElapsedTime += deltaTime;

        // 全局 Buff 节拍：每满一个间隔所有 Buff 同时结算一跳
        _buffTickRemaining -= deltaTime;
        int buffJumps = 0;
        while (_buffTickRemaining <= 0) {
            _buffTickRemaining += BuffTickInterval;
            buffJumps++;
        }

        _eventLog.Clear();
        _eventLog.AppendRange(_pendingEvents);
        _pendingEvents.Clear();

        // 移动结算统一在领域层：本帧输入本帧生效，服务端与回放同源同序。
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

        return _eventLog;
    }

    /// <summary>
    /// 移动结算统一在领域层：本帧输入本帧生效，服务端、在线与回放同源同序。
    /// 意图集合只含存活且有位移输入的单位；静止与死亡单位不参与互斥，不作为他人的障碍。
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

    /// <summary>推进单位读条；读条完成时结算技能并清理读条状态。</summary>
    private void TickCasting(BattleUnit unit, float deltaTime, BattleEventLog log) {
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
        Message = "[BattleScene] {Enemy} cannot find skill {SkillId}.")]
    private partial void LogSkillNotFound(string enemy, string skillId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[BattleScene] {Enemy} cast rejected: {SkillId} on {Target}.")]
    private partial void LogCastRejected(string enemy, string skillId, string target);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[BattleScene] {Enemy} starts casting skill {SkillId} on {Target}.")]
    private partial void LogCastStarted(string enemy, string skillId, string target);

    #endregion
}

