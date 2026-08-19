using System.Numerics;
using DungeonChessBattle.Battle.Domain;
using DungeonChessBattle.Battle.Domain.Buffs;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Combat.Hates;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Domain.Intelligence;
using DungeonChessBattle.Battle.Domain.Movement;
using DungeonChessBattle.Battle.Logic.Buffs;
using DungeonChessBattle.Battle.Logic.Combat;
using DungeonChessBattle.Battle.Logic.Hates;
using DungeonChessBattle.Battle.Logic.Movement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
// 过渡期：Battle.Logic 全局 using 旧 Battle.Enums 含同名 BattlePhase，此处显式指向 Domain 权威枚举。
// 删除旧 Battle 项目后该别名可一并移除。
using BattlePhase = DungeonChessBattle.Battle.Domain.Combat.BattlePhase;

namespace DungeonChessBattle.Battle.Logic;

/// <summary>
/// 战斗世界实现：实现 <see cref="IBattleScene"/>，统一驱动读条、冷却、Buff、仇恨、技能结算与 AI 决策。
/// 面向 Domain 接口 IBattleUnit 读写单位状态、经 IBattleRoom 读写房间阶段，不依赖网络载体与全局配置仓库。
/// 单位权威状态经 <see cref="IBattleUnit.RuntimeState"/> 承载于单位自身，本类只做推进、投影与结算。
/// <see cref="ApplyDecisions"/> 在实体移动结算前调用以注入 AI 输入，
/// <see cref="Tick"/> 在实体更新后调用推进战斗并返回领域事件做网络广播。
/// 领域事件是唯一真相源：本类把事件流交给各单位仇恨规则分发推衍。
/// </summary>
/// <param name="relations">副本配置的阵营关系函数，由房间按副本装配。</param>
/// <param name="movementScene">竞技场移动场景，由房间按副本布局构建注入，与战斗世界同生命周期。</param>
/// <param name="hateSettings">仇恨系统参数，可选覆盖。</param>
/// <param name="logger">AI 决策日志，可选注入。</param>
public sealed partial class BattleScene(
    CampRelationResolver relations,
    PhysicsMovementScene movementScene,
    HateSettings? hateSettings = null,
    ILogger<BattleScene>? logger = null) : IBattleScene {
    /// <summary>副本配置的阵营关系函数，敌我判定的唯一来源。</summary>
    private readonly CampRelationResolver _relations = relations;

    /// <summary>仇恨系统参数，未注入时用默认。</summary>
    private readonly HateSettings _hateSettings = hateSettings ?? new HateSettings();

    /// <summary>AI 决策与应用日志，未注入时用 NullLogger 静默。</summary>
    private readonly ILogger<BattleScene> _logger = logger ?? NullLogger<BattleScene>.Instance;

    /// <summary>竞技场移动场景：静态障碍与单位互斥的空间载体，构造后只读，与战斗世界同生命周期。</summary>
    private readonly PhysicsMovementScene _movementScene = movementScene;

    /// <inheritdoc />
    public IMovementScene MovementScene => _movementScene;

    /// <summary>服务端权威仇恨账本。</summary>
    private readonly HateSystem _hates = new();

    /// <summary>网络 ID 到单位的索引，AI 目标查询与仇恨投影用。</summary>
    private readonly Dictionary<ushort, IBattleUnit> _unitById = [];

    private readonly List<IBattleUnit> _units = [];

    /// <inheritdoc />
    public IReadOnlyList<IBattleUnit> Units => _units;

    /// <inheritdoc />
    public IBattleUnit? FindUnit(ushort netId) =>
        _unitById.TryGetValue(netId, out var unit) ? unit : null;

    /// <summary>已判定死亡的单位，避免重复触发 UnitDied。</summary>
    private readonly HashSet<IBattleUnit> _dead = [];

    /// <summary>房间级战斗状态载体，首帧由编排层经 BindRoom 注入；阶段权威经载体读写，绑定先于任何阶段操作。</summary>
    private IBattleRoom? _battleRoom;

    /// <summary>绑定房间级战斗状态载体，首帧由编排层注入；绑定先于任何阶段操作。</summary>
    public void BindRoom(IBattleRoom room) {
        ArgumentNullException.ThrowIfNull(room);
        _battleRoom = room;
    }

    /// <summary>房间级状态载体访问器：未绑定属配置故障，阶段操作响亮失败而非静默。</summary>
    private IBattleRoom BattleRoom => _battleRoom
        ?? throw new InvalidOperationException("BattleScene 未绑定房间级状态载体，任何阶段操作前必须 BindRoom。");

    /// <summary>当前战斗阶段，经 IBattleRoom 读取载体权威，未绑定视为 Waiting；非 Running 时 Tick 不推进。</summary>
    public BattlePhase CurrentPhase => _battleRoom?.CurrentPhase ?? BattlePhase.Waiting;

    /// <summary>战斗已运行的秒数，Running 期间累加。</summary>
    public float ElapsedTime {
        get; private set;
    }

    /// <summary>已判定结束，避免重复执行结束判定。</summary>
    private bool _ended;

    /// <summary>Buff 全局结算间隔，秒；所有存活 Buff 在同一节拍点同时结算。</summary>
    private const double BuffTickInterval = 3.0;

    /// <summary>距下一次 Buff 全局结算的剩余时间，秒。</summary>
    private double _buffTickRemaining = BuffTickInterval;

    /// <summary>注册一个战斗单位到战斗世界，空间演员注册与战斗注册同生命周期。</summary>
    public void AddUnit(IBattleUnit unit) {
        ArgumentNullException.ThrowIfNull(unit);
        if (!_units.Contains(unit)) {
            _units.Add(unit);
            _unitById[unit.UnitNetId] = unit;
            _movementScene.AddActor(unit.UnitNetId,
                () => unit.Snapshot.BodyRadius, () => unit.Snapshot.Position);
        }
    }

    /// <summary>移除已注册的战斗单位与空间演员。权威状态随单位载体生命周期，无需额外清理。</summary>
    public void RemoveUnit(IBattleUnit unit) {
        _units.Remove(unit);
        _unitById.Remove(unit.UnitNetId);
        _dead.Remove(unit);
        _hates.RemoveUnit(unit.UnitNetId);
        _movementScene.RemoveActor(unit.UnitNetId);
    }

    /// <summary>
    /// 开始战斗：Waiting 到 Running，清零计时，阶段状态经载体写入。
    /// </summary>
    public void StartBattle() {
        if (CurrentPhase != BattlePhase.Waiting)
            return;

        BattleRoom.ProjectBattleStarted();
        ElapsedTime = 0f;
        _buffTickRemaining = BuffTickInterval;
    }

    /// <summary>
    /// 手动结束战斗，幂等兜底，如全员断线。阶段状态经载体写入。
    /// </summary>
    public void EndBattle() {
        if (CurrentPhase == BattlePhase.Finished)
            return;
        BattleRoom.ProjectBattleEnded();
    }

    /// <summary>
    /// 发起读条施法：技能存在、归属、状态与目标/位置校验通过后写入读条状态并暂存目标。
    /// </summary>
    /// <returns>校验通过并成功发起返回 true。</returns>
    public bool BeginCast(IBattleUnit caster, SkillKeyId skillKey, IBattleUnit? target, Vector2? targetPos) {
        var skill = caster.GetSkill(skillKey);
        if (skill == null)
            return false;
        if (!SkillCastValidator.CanCast(caster, skill, target, targetPos, _relations))
            return false;

        caster.SkillCasting = skillKey;
        caster.SkillCastRemaining = skill.SpellTime;
        caster.RuntimeState.CastTarget = target;
        caster.RuntimeState.CastTargetPos = targetPos;
        return true;
    }

    /// <summary>
    /// 单位发生移动：保留既定行为"移动即打断读条"。
    /// </summary>
    public void OnUnitMoved(IBattleUnit unit, Vector2 moveDir) {
        if (moveDir.LengthSquared() <= 0.0001f || unit.SkillCasting == default)
            return;
        unit.SkillCasting = default;
        unit.SkillCastRemaining = 0f;
        unit.RuntimeState.ClearCast();
    }

    /// <summary>
    /// AI 前置推进：为全部带智能的存活单位决策并应用移动输入与施法请求。
    /// 本帧单位列表只读，决策输入经 <see cref="Units"/> 取本帧权威状态。
    /// </summary>
    public void ApplyDecisions() {
        if (CurrentPhase != BattlePhase.Running)
            return;

        foreach (var unit in _units) {
            if (unit.Health <= 0f || unit.Intelligence is not { } intelligence)
                continue;

            var decision = intelligence.Decide(unit, this, _relations);
            ApplyDecision(unit, decision);
        }
    }

    /// <summary>把 AI 决策映射为世界动作：停止、逼近或发起施法。</summary>
    private void ApplyDecision(IBattleUnit enemy, EnemyDecision decision) {
        switch (decision.Kind) {
            case EnemyDecisionKind.Idle:
                enemy.SetMovementInput(Vector2.Zero);
                break;

            case EnemyDecisionKind.MoveTo:
                enemy.SetMovementInput(decision.MoveDirection);
                OnUnitMoved(enemy, decision.MoveDirection);
                break;

            case EnemyDecisionKind.CastSkill:
                enemy.SetMovementInput(Vector2.Zero);
                TryBeginCast(enemy, decision);
                break;

            default:
                LogUnknownDecision(enemy.UnitName, decision.Kind);
                break;
        }
    }

    /// <summary>按技能目标类型解析单位目标后发起读条；目标丢失或校验失败仅记日志，下一帧重新决策。</summary>
    private void TryBeginCast(IBattleUnit enemy, EnemyDecision decision) {
        var skill = enemy.GetSkill(decision.SkillId);
        if (skill == null) {
            LogSkillNotFound(enemy.UnitName, decision.SkillId.Id);
            return;
        }

        IBattleUnit? target = null;
        if (skill.NeedUnitTarget) {
            var targetUnit = FindUnit(decision.TargetNetId);
            if (targetUnit == null)
                return;
            target = targetUnit;
        }

        string targetName = target?.UnitName ?? "(position)";
        if (!BeginCast(enemy, decision.SkillId, target, decision.TargetPosition)) {
            LogCastRejected(enemy.UnitName, decision.SkillId.Id, targetName);
            return;
        }

        LogCastStarted(enemy.UnitName, decision.SkillId.Id, targetName);
    }

    /// <summary>
    /// 按帧推进全部单位的读条、冷却与 Buff，返回本帧领域事件。
    /// 仅在 Running 阶段推进；战斗结束条件满足时经载体切换 Finished。
    /// </summary>
    public IReadOnlyList<IDomainEvent> Tick(double deltaTime) {
        if (CurrentPhase != BattlePhase.Running)
            return [];

        ElapsedTime += (float)deltaTime;

        // 全局 Buff 节拍：每满一个间隔所有 Buff 同时结算一跳
        _buffTickRemaining -= deltaTime;
        int buffJumps = 0;
        while (_buffTickRemaining <= 0) {
            _buffTickRemaining += BuffTickInterval;
            buffJumps++;
        }

        var events = new List<IDomainEvent>();
        foreach (var unit in _units.ToArray()) {
            TickCasting(unit, deltaTime, events);
            TickCooldowns(unit, deltaTime);
            TickBuffs(unit, deltaTime, events, buffJumps);
        }

        // 全量死亡扫描：本帧内所有 Health<=0 的单位统一产出 UnitDied。
        // 若在单位迭代内判定，同帧互杀时后死者可能因战斗已切 Finished 而丢失死亡事件。
        // 仇恨账本清理延迟到增量推衍之后，避免本帧死者因自身伤害事件重写进账本。
        foreach (var unit in _units.ToArray()) {
            if (unit.Health <= 0f && _dead.Add(unit))
                events.Add(new UnitDied(unit.UnitNetId));
        }

        TryEndBattle();

        // 事件流单一消费点：先按单位自身仇恨规则求效果并落账；仇恨状态经投影同步
        foreach (var effect in HateDispatcher.Dispatch(events, _units, _unitById, _hateSettings, _relations))
            _hates.ApplyEffect(effect);

        // 死亡清理：本帧死者从仇恨账本移除，含其持有表与他表对其条目，保证死者不残留
        foreach (var unit in _units.ToArray()) {
            if (unit.Health <= 0f)
                _hates.RemoveUnit(unit.UnitNetId);
        }
        ProjectHates();

        return events;
    }

    /// <summary>
    /// 判定战斗是否结束：任一阵营无存活单位则结束；满足条件时经载体置 Finished，每场仅执行一次。
    /// </summary>
    private void TryEndBattle() {
        if (CurrentPhase != BattlePhase.Running || _ended)
            return;

        var allCamps = _units.SelectMany(u => u.Camps).Distinct().ToHashSet();
        if (allCamps.Count < 2)
            return;

        var aliveCamps = _units.Where(u => u.Health > 0f).SelectMany(u => u.Camps).Distinct().ToHashSet();
        if (aliveCamps.Count >= allCamps.Count)
            return;

        _ended = true;
        BattleRoom.ProjectBattleEnded();
    }

    #region Tick 内部

    private void TickCasting(IBattleUnit unit, double deltaTime, List<IDomainEvent> events) {
        if (unit.SkillCasting == default)
            return;

        unit.SkillCastRemaining -= (float)deltaTime;
        if (unit.SkillCastRemaining > 0f)
            return;

        ResolveCast(unit, events);
        unit.SkillCasting = default;
        unit.SkillCastRemaining = 0f;
        unit.RuntimeState.ClearCast();
    }

    private static void TickCooldowns(IBattleUnit unit, double deltaTime) {
        var entries = unit.RuntimeState.Cooldowns;
        if (entries.Count == 0)
            return;
        float dt = (float)deltaTime;
        for (int i = entries.Count - 1; i >= 0; i--) {
            CooldownEntry entry = entries[i];
            float remaining = entry.Remaining - dt;
            if (remaining <= 0f) {
                entries.RemoveAt(i);
                unit.SetSkillCooldown(entry.SkillKey, 0f);
            }
            else {
                entry.Remaining = remaining;
                // 剩余时间由客户端按 EndServerTick 本地推算，不再每 tick 写载体
            }
        }
    }

    private static void TickBuffs(IBattleUnit target, double deltaTime, List<IDomainEvent> events, int buffJumps) {
        var list = target.RuntimeState.Buffs;
        if (list.Count == 0)
            return;

        double tickSeconds = buffJumps * BuffTickInterval;
        var snapshot = target.Snapshot;
        var alive = new List<ActiveBuff>(list.Count);
        foreach (var buff in list) {
            foreach (var e in BuffTickProcessor.Tick(buff.Effect, buff.Instance, snapshot, deltaTime, tickSeconds)) {
                events.Add(e);
                if (e is DamageOccurred dmg)
                    ApplyHealthDelta(target, -dmg.AppliedDamage);
                else if (e is HealOccurred heal)
                    ApplyHealthDelta(target, heal.ActualHeal);
            }
            if (buff.Instance.IsAlive)
                alive.Add(buff);
        }

        // 仅结构变化时投影载体：新增与叠加在 AddBuff 投影，到期在此投影。
        // 剩余时间由客户端按 EndServerTick 本地推算，不随每 tick 递减同步。
        if (alive.Count != list.Count) {
            list.Clear();
            list.AddRange(alive);
            ProjectBuffs(target);
        }
    }

    /// <summary>把目标单位的权威 Buff 列表全量投影到载体，低频结构变化时调用。</summary>
    private static void ProjectBuffs(IBattleUnit target) {
        var list = target.RuntimeState.Buffs;
        if (list.Count == 0) {
            target.ReplaceBuffs([]);
            return;
        }
        target.ReplaceBuffs([.. list.Select(b => new BuffView {
            BuffTypeId = b.Instance.BuffTypeId,
            Remaining = (float)b.Instance.Remaining,
            StackCount = (ushort)b.Instance.Stacks,
            DamageType = EffectDamageType(b.Effect),
        })]);
    }

    private void ResolveCast(IBattleUnit caster, List<IDomainEvent> events) {
        var state = caster.RuntimeState;
        if (state.CastTarget is null && state.CastTargetPos is null)
            return;

        var skill = caster.GetSkill(caster.SkillCasting);
        if (skill == null)
            return;

        // 读条完成：写入权威个体冷却并推进全局冷却
        SetCooldownAuthoritative(caster, caster.SkillCasting, skill.CooldownTime);
        caster.GcdRemaining = MathF.Max(caster.GcdRemaining, skill.GcdTime);

        switch (skill) {
            case DamageSkillDefinition d:
                if (state.CastTarget is { } target) {
                    var res = CastResolver.ComputeDamage(caster.Snapshot, target.Snapshot, d.Damage, d.DamageType);
                    target.Health = res.RemainingHealth;
                    events.Add(new DamageOccurred(caster.UnitNetId, target.UnitNetId, res.AppliedDamage, d.DamageType));
                }
                break;

            case HealSkillDefinition h:
                if (state.CastTarget is { } healTarget) {
                    var heal = CastResolver.ComputeHeal(caster.Snapshot, healTarget.Snapshot, h.CurePotency);
                    healTarget.Health = heal.RemainingHealth;
                    events.Add(new HealOccurred(caster.UnitNetId, healTarget.UnitNetId, heal.ActualHeal));
                }
                break;

            case RangeDamageSkillDefinition r:
                ResolveRangeDamage(caster, r, state.CastTargetPos, events);
                break;

            case AddBuffSkillDefinition ab:
                if (state.CastTarget is { } buffTarget)
                    AddBuff(buffTarget, ab.Buff, caster, events);
                break;

            case HateSkillDefinition t:
                if (state.CastTarget is { } hateTarget)
                    events.Add(new HateRequested(hateTarget.UnitNetId, caster.UnitNetId, t.Op, t.Value));
                break;
        }

        events.Add(new CastCompleted(caster.UnitNetId, skill.SkillId, state.CastTarget?.UnitNetId));
    }

    /// <summary>写入单位的权威个体冷却，同技能已有冷却时刷新取较大值；权威变化立即投影回载体，保证施放校验即时生效。</summary>
    private static void SetCooldownAuthoritative(IBattleUnit unit, SkillKeyId skillKey, float remaining) {
        var entries = unit.RuntimeState.Cooldowns;
        foreach (var entry in entries) {
            if (entry.SkillKey != skillKey)
                continue;
            if (remaining <= entry.Remaining)
                return;
            entry.Remaining = remaining;
            unit.SetSkillCooldown(skillKey, remaining);
            return;
        }
        entries.Add(new CooldownEntry(skillKey, remaining));
        unit.SetSkillCooldown(skillKey, remaining);
    }

    private void ResolveRangeDamage(IBattleUnit caster, RangeDamageSkillDefinition skill, Vector2? targetPos, List<IDomainEvent> events) {
        var aim = (targetPos ?? Vector2.Zero) - caster.Snapshot.Position;
        foreach (var unit in _units.ToArray()) {
            if (unit == caster || !SkillTargetValidator.CanAffect(caster, unit, skill.TargetPolicy, _relations))
                continue;
            if (!CastResolver.IsInRange(skill.Range, caster.Snapshot, unit.Snapshot, aim))
                continue;

            var res = CastResolver.ComputeDamage(caster.Snapshot, unit.Snapshot, skill.Damage, skill.DamageType);
            unit.Health = res.RemainingHealth;
            events.Add(new DamageOccurred(caster.UnitNetId, unit.UnitNetId, res.AppliedDamage, skill.DamageType));
        }
    }

    private static void AddBuff(IBattleUnit target, BuffDefinition def, IBattleUnit caster, List<IDomainEvent> events) {
        var list = target.RuntimeState.Buffs;
        var existing = list.FirstOrDefault(b => b.Instance.BuffTypeId == def.BuffTypeId);
        int stacks;
        if (existing != null) {
            existing.Instance.Remaining = Math.Max(existing.Instance.Remaining, def.Duration);
            existing.Instance.Stacks = Math.Min(existing.Instance.Stacks + 1, def.MaxStacks);
            stacks = existing.Instance.Stacks;
        }
        else {
            list.Add(new ActiveBuff(BuffFactory.CreateInstance(def, target.UnitNetId, caster.Snapshot, caster.UnitNetId),
                BuffFactory.CreateEffect(def)));
            stacks = 1;
        }

        events.Add(new BuffApplied(target.UnitNetId, def.BuffTypeId, stacks));
        ProjectBuffs(target);
    }

    private static void ApplyHealthDelta(IBattleUnit unit, float delta) {
        unit.Health = Math.Clamp(unit.Health + delta, 0f, unit.MaxHealth);
    }

    private static byte EffectDamageType(IBuffEffect effect) => effect switch {
        DotEffect dot => (byte)dot.DamageType,
        _ => 0,
    };

    /// <summary>把脏仇恨表全量投影到单位载体，供网络同步。无变化的单位被跳过。</summary>
    private void ProjectHates() {
        foreach (var holderId in _hates.GetDirtyAndClear()) {
            if (!_unitById.TryGetValue(holderId, out var holder))
                continue;
            holder.ReplaceHates(_hates.Snapshot(holderId));
        }
    }

    #endregion

    #region 日志

    [LoggerMessage(Level = LogLevel.Error,
        Message = "[BattleScene] Unknown enemy decision kind: {Enemy} -> {Kind}.")]
    private partial void LogUnknownDecision(string enemy, EnemyDecisionKind kind);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "[BattleScene] {Enemy} cannot find skill {SkillId}.")]
    private partial void LogSkillNotFound(string enemy, ushort skillId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[BattleScene] {Enemy} cast rejected: {SkillId} on {Target}.")]
    private partial void LogCastRejected(string enemy, ushort skillId, string target);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[BattleScene] {Enemy} starts casting skill {SkillId} on {Target}.")]
    private partial void LogCastStarted(string enemy, ushort skillId, string target);

    #endregion
}
