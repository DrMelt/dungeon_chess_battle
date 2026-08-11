using System.Numerics;
using DungeonChessBattle.Battle.Domain.Buffs;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Domain.Movement;
using DungeonChessBattle.Battle.Logic.Buffs;
using DungeonChessBattle.Battle.Logic.Combat;
// 过渡期：Battle.Logic 全局 using 旧 Battle.Enums 含同名 BattlePhase，此处显式指向 Domain 权威枚举。
// 删除旧 Battle 项目后该别名可一并移除。
using BattlePhase = DungeonChessBattle.Battle.Domain.Combat.BattlePhase;

namespace DungeonChessBattle.Battle.Logic;

/// <summary>
/// 战斗编排门面：统一驱动读条、冷却、Buff 推进与技能结算。
/// 只依赖 Domain 接口 IBattleUnit、ISkillRepository 与 IMovementScene，不依赖具体网络载体。
/// 服务端每帧调用 <see cref="Tick"/> 推进状态，并消费返回的领域事件做网络广播。
/// </summary>
public sealed class BattleRoom(ISkillRepository skills) {
    private readonly ISkillRepository _skills = skills ?? throw new ArgumentNullException(nameof(skills));
    private readonly List<IBattleUnit> _units = [];

    /// <summary>读条目标权威，服务端私有，不参与同步。</summary>
    private readonly Dictionary<IBattleUnit, CastContext> _castTargets = [];

    /// <summary>运行时 Buff 权威，按目标单位分组。</summary>
    private readonly Dictionary<IBattleUnit, List<ActiveBuff>> _buffs = [];

    /// <summary>已判定死亡的单位，避免重复触发 UnitDied。</summary>
    private readonly HashSet<IBattleUnit> _dead = [];

    /// <summary>当前战斗阶段，阶段机权威，非 Running 时 Tick 不推进。</summary>
    public BattlePhase CurrentPhase { get; private set; } = BattlePhase.Waiting;

    /// <summary>战斗已运行的秒数，Running 期间累加。</summary>
    public float ElapsedTime {
        get; private set;
    }

    /// <summary>已判定结束，避免重复产出 BattleEnded。</summary>
    private bool _ended;

    private sealed record CastContext(IBattleUnit? Target, Vector2? TargetPos);

    private sealed record ActiveBuff(BuffInstance Instance, IBuffEffect Effect);

    /// <summary>注册一个战斗单位到编排门面。</summary>
    public void AddUnit(IBattleUnit unit) {
        ArgumentNullException.ThrowIfNull(unit);
        if (!_units.Contains(unit))
            _units.Add(unit);
    }

    /// <summary>移除已注册的战斗单位。</summary>
    public void RemoveUnit(IBattleUnit unit) {
        _units.Remove(unit);
        _castTargets.Remove(unit);
        _buffs.Remove(unit);
        _dead.Remove(unit);
    }

    /// <summary>
    /// 开始战斗：Waiting 到 Running，清零计时。返回本步产生的领域事件，含 <see cref="BattleStarted"/>。
    /// </summary>
    public IReadOnlyList<IDomainEvent> StartBattle() {
        if (CurrentPhase != BattlePhase.Waiting)
            return [];

        CurrentPhase = BattlePhase.Running;
        ElapsedTime = 0f;
        return [new BattleStarted()];
    }

    /// <summary>
    /// 手动结束战斗，幂等兜底，如全员断线。产生战斗内事件，由编排层自行按需消费。
    /// </summary>
    public void EndBattle() {
        if (CurrentPhase == BattlePhase.Finished)
            return;
        CurrentPhase = BattlePhase.Finished;
    }

    /// <summary>
    /// 发起读条施法：冷却校验通过后写入读条状态并暂存目标。
    /// </summary>
    /// <returns>冷却校验通过并成功发起返回 true。</returns>
    public bool BeginCast(IBattleUnit caster, ushort skillId, IBattleUnit? target, Vector2? targetPos) {
        if (caster.GcdRemaining > 0f)
            return false;
        if (caster.SkillCooldowns.TryGetValue(skillId, out var remaining) && remaining > 0f)
            return false;

        var skill = _skills.Get(skillId);
        if (skill == null)
            return false;

        caster.SkillCasting = skillId;
        caster.SkillCastRemaining = skill.SpellTime;
        _castTargets[caster] = new CastContext(target, targetPos);
        return true;
    }

    /// <summary>
    /// 单位发生移动：保留既定行为"移动即打断读条"。
    /// </summary>
    public void OnUnitMoved(IBattleUnit unit, Vector2 moveDir) {
        if (moveDir.LengthSquared() <= 0.0001f || unit.SkillCasting == 0)
            return;
        unit.SkillCasting = 0;
        unit.SkillCastRemaining = 0f;
        _castTargets.Remove(unit);
    }

    /// <summary>
    /// 按帧推进全部单位的读条、冷却与 Buff，返回本帧领域事件。
    /// 仅在 Running 阶段推进；战斗结束条件满足时产出 <see cref="BattleEnded"/> 并切换 Finished。
    /// </summary>
    public IReadOnlyList<IDomainEvent> Tick(double deltaTime) {
        if (CurrentPhase != BattlePhase.Running)
            return [];

        ElapsedTime += (float)deltaTime;
        var events = new List<IDomainEvent>();
        foreach (var unit in _units.ToArray()) {
            TickCasting(unit, deltaTime, events);
            TickCooldowns(unit, deltaTime);
            TickBuffs(unit, deltaTime, events);
        }

        // 全量死亡扫描：本帧内所有 Health<=0 的单位统一产出 UnitDied。
        // 若在单位迭代内判定，同帧互杀时后死者可能因战斗已切 Finished 而丢失死亡事件。
        foreach (var unit in _units.ToArray()) {
            if (unit.Health <= 0f && _dead.Add(unit))
                events.Add(new UnitDied(unit.UnitNetId));
        }

        if (TryEndBattle(out string? winnerCamp))
            events.Add(new BattleEnded(winnerCamp));
        return events;
    }

    /// <summary>
    /// 判定战斗是否结束：任一阵营无存活单位则结束。胜方为仍有存活单位的唯一阵营，否则平局/无存活。
    /// 满足条件时置 Finished 并返回 true，每场仅产出一次 BattleEnded。
    /// </summary>
    private bool TryEndBattle(out string? winnerCamp) {
        winnerCamp = null;
        if (CurrentPhase != BattlePhase.Running || _ended)
            return false;

        var allCamps = _units.SelectMany(u => u.Camps).Distinct().ToHashSet();
        if (allCamps.Count < 2)
            return false;

        var aliveCamps = _units.Where(u => u.Health > 0f).SelectMany(u => u.Camps).Distinct().ToHashSet();
        if (aliveCamps.Count >= allCamps.Count)
            return false;

        _ended = true;
        CurrentPhase = BattlePhase.Finished;
        winnerCamp = aliveCamps.Count == 1 ? aliveCamps.Single() : null;
        return true;
    }

    #region Tick 内部

    private void TickCasting(IBattleUnit unit, double deltaTime, List<IDomainEvent> events) {
        if (unit.SkillCasting == 0)
            return;

        unit.SkillCastRemaining -= (float)deltaTime;
        if (unit.SkillCastRemaining > 0f)
            return;

        ResolveCast(unit, events);
        unit.SkillCasting = 0;
        unit.SkillCastRemaining = 0f;
        _castTargets.Remove(unit);
    }

    private static void TickCooldowns(IBattleUnit unit, double deltaTime) {
        if (unit.GcdRemaining > 0f)
            unit.GcdRemaining = MathF.Max(0f, unit.GcdRemaining - (float)deltaTime);

        if (unit.SkillCooldowns.Count == 0)
            return;
        foreach (var kv in unit.SkillCooldowns.ToArray()) {
            var remaining = kv.Value - (float)deltaTime;
            unit.SetSkillCooldown(kv.Key, MathF.Max(0f, remaining));
        }
    }

    private void TickBuffs(IBattleUnit target, double deltaTime, List<IDomainEvent> events) {
        if (!_buffs.TryGetValue(target, out var list) || list.Count == 0)
            return;

        var snapshot = target.Snapshot;
        var alive = new List<ActiveBuff>(list.Count);
        foreach (var buff in list) {
            foreach (var e in BuffTickProcessor.Tick(buff.Effect, buff.Instance, snapshot, deltaTime)) {
                events.Add(e);
                if (e is DamageOccurred dmg)
                    ApplyHealthDelta(target, -dmg.AppliedDamage);
                else if (e is HealOccurred heal)
                    ApplyHealthDelta(target, heal.ActualHeal);
            }
            if (buff.Instance.IsAlive)
                alive.Add(buff);
        }

        if (alive.Count != list.Count)
            _buffs[target] = alive;

        target.ReplaceBuffs([.. alive.Select(b => new BuffView {
            BuffTypeId = b.Instance.BuffTypeId,
            Remaining = (float)b.Instance.Remaining,
            StackCount = (ushort)b.Instance.Stacks,
            DamageType = EffectDamageType(b.Effect),
        })]);
    }

    private void ResolveCast(IBattleUnit caster, List<IDomainEvent> events) {
        if (!_castTargets.TryGetValue(caster, out var ctx))
            return;

        var skill = _skills.Get(caster.SkillCasting);
        if (skill == null)
            return;

        // 读条完成：写入个体冷却与全局冷却
        caster.SetSkillCooldown(caster.SkillCasting, skill.CooldownTime);
        caster.GcdRemaining = MathF.Max(caster.GcdRemaining, skill.GcdTime);

        switch (skill) {
            case DamageSkillDefinition d:
                if (ctx.Target is { } target) {
                    var res = CastResolver.ComputeDamage(caster.Snapshot, target.Snapshot, d.Damage, d.DamageType);
                    target.Health = res.RemainingHealth;
                    events.Add(new DamageOccurred(target.UnitNetId, res.AppliedDamage, d.DamageType));
                }
                break;

            case HealSkillDefinition h:
                if (ctx.Target is { } healTarget) {
                    var heal = CastResolver.ComputeHeal(caster.Snapshot, healTarget.Snapshot, h.CurePotency);
                    healTarget.Health = heal.RemainingHealth;
                    events.Add(new HealOccurred(healTarget.UnitNetId, heal.ActualHeal));
                }
                break;

            case RangeDamageSkillDefinition r:
                ResolveRangeDamage(caster, r, ctx.TargetPos, events);
                break;

            case AddBuffSkillDefinition ab:
                if (ctx.Target is { } buffTarget)
                    AddBuff(buffTarget, ab.Buff, caster, events);
                break;
        }

        events.Add(new CastCompleted(caster.UnitNetId, skill.SkillId, ctx.Target?.UnitNetId));
    }

    private void ResolveRangeDamage(IBattleUnit caster, RangeDamageSkillDefinition skill, Vector2? targetPos, List<IDomainEvent> events) {
        var aim = (targetPos ?? Vector2.Zero) - caster.Snapshot.Position;
        foreach (var unit in _units.ToArray()) {
            if (unit == caster || IsSameCamp(caster, unit))
                continue;
            if (!CastResolver.IsInRange(skill.Range, caster.Snapshot, unit.Snapshot, aim))
                continue;

            var res = CastResolver.ComputeDamage(caster.Snapshot, unit.Snapshot, skill.Damage, skill.DamageType);
            unit.Health = res.RemainingHealth;
            events.Add(new DamageOccurred(unit.UnitNetId, res.AppliedDamage, skill.DamageType));
        }
    }

    private void AddBuff(IBattleUnit target, BuffDefinition def, IBattleUnit caster, List<IDomainEvent> events) {
        if (!_buffs.TryGetValue(target, out var list)) {
            list = [];
            _buffs[target] = list;
        }

        var existing = list.FirstOrDefault(b => b.Instance.BuffTypeId == def.BuffTypeId);
        int stacks;
        if (existing != null) {
            existing.Instance.Remaining = System.Math.Max(existing.Instance.Remaining, def.Duration);
            existing.Instance.Stacks = System.Math.Min(existing.Instance.Stacks + 1, def.MaxStacks);
            stacks = existing.Instance.Stacks;
        }
        else {
            list.Add(new ActiveBuff(BuffFactory.CreateInstance(def, target.UnitNetId, caster.Snapshot),
                BuffFactory.CreateEffect(def)));
            stacks = 1;
        }

        events.Add(new BuffApplied(target.UnitNetId, def.BuffTypeId, stacks));
    }

    private static void ApplyHealthDelta(IBattleUnit unit, float delta) {
        unit.Health = System.Math.Clamp(unit.Health + delta, 0f, unit.MaxHealth);
    }

    private static bool IsSameCamp(IBattleUnit a, IBattleUnit b)
        => a.Camps.Any(c => b.Camps.Contains(c));

    private static byte EffectDamageType(IBuffEffect effect) => effect switch {
        DotEffect dot => (byte)dot.DamageType,
        _ => 0,
    };

    #endregion
}
