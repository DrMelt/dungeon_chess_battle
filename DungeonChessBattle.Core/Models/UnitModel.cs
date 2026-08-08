using System.Numerics;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 单位数据模型，实现 IUnitState。
/// 仅承载运行时数据与基础数值计算（伤害/治疗公式），不驱动行为循环。
/// Buff 更新、技能 GCD 周转等行为由外部调用方（Logic BattleResolver / Godot UnitState）按需通过接口方法触发。
/// </summary>
public class UnitModel : IUnitState {
    #region Identity & Camp

    /// <summary>单位名称（调试与事件标识用）。</summary>
    public string UnitStateName { get; set; } = "UnitStateName";

    /// <summary>单位碰撞半径，用于技能范围判定。</summary>
    public float BodyRadius { get; set; } = 1.0f;

    /// <summary>单位所属阵营列表。</summary>
    public List<string> Camps { get; set; } = [];

    #endregion

    #region Health & Shield

    /// <summary>最大生命值。</summary>
    public float MaxHealth {
        get;
        set {
            if (System.MathF.Abs(value - field) > 0.0001f) {
                field = value;
                MaxHealthChanged?.Invoke(field);
            }
        }
    } = 1000;

    private float _health = 1000;

    /// <summary>当前生命值。</summary>
    public float Health {
        get => _health;
        set {
            if (System.MathF.Abs(value - _health) > 0.0001f) {
                _health = value;
                HealthChanged?.Invoke(_health);
            }
        }
    }

    /// <summary>护盾值（当前版本固定为 0）。</summary>
    public static float Shield => 0.0f;

    /// <summary>生命 + 护盾总值。</summary>
    public float HealthShield => Health + Shield;

    /// <summary>剩余生命百分比（0~1）。</summary>
    public float HealthPercent => Health / System.MathF.Max(MaxHealth, HealthShield);

    /// <summary>生命 + 护盾与最大生命之比（0~1）。</summary>
    public float HealthShieldPercent => HealthShield / MaxHealth;

    /// <summary>治疗强度系数（治疗量倍率）。</summary>
    public float CureIntensity {
        get; set;
    }

    /// <summary>物理攻击基础系数（伤害倍率）。</summary>
    public float PhysicalAttackBase {
        get; set;
    }

    /// <summary>物理伤害承受系数（减免倍率）。</summary>
    public float PhysicalTakePercent {
        get; set;
    }

    /// <summary>魔法攻击基础系数（伤害倍率）。</summary>
    public float MagicAttackBase {
        get; set;
    }

    /// <summary>魔法伤害承受系数（减免倍率）。</summary>
    public float MagicTakePercent {
        get; set;
    }

    /// <summary>基础移动速度。</summary>
    public float BaseSpeed {
        get; set;
    }

    /// <summary>当前移动速度（等于基础速度）。</summary>
    public float MoveSpeed => BaseSpeed;

    private Vector3 _position = Vector3.Zero;

    /// <summary>单位当前世界坐标。</summary>
    public Vector3 Position => _position;

    /// <summary>单位朝向向量。</summary>
    public Vector3 LookAtDir { get; set; } = new Vector3(0, 0, 1);

    /// <summary>剩余全局冷却时间（秒）。</summary>
    public float GcdTime {
        get; set;
    }

    /// <summary>单位拥有的技能列表。</summary>
    public List<IUnitSkill> SkillsList { get; set; } = [];

    /// <summary>当前正在施放的技能。</summary>
    public IUnitSkill? SpellingSkill {
        get; private set;
    }

    /// <summary>单位当前持有的 Buff 列表。</summary>
    public List<IBuff> BuffList { get; private set; } = [];

    /// <summary>仇恨表（单位名 → 仇恨值）。</summary>
    public Dictionary<string, float> Hates { get; set; } = [];

    #endregion

    /// <summary>当前生命值变化事件（参数为变化后的生命值）。</summary>
    public event Action<float>? HealthChanged;

    /// <summary>最大生命值变化事件（参数为变化后的最大生命值）。</summary>
    public event Action<float>? MaxHealthChanged;

    /// <summary>受到伤害事件（参数为目标、实际伤害量、伤害类型）。</summary>
    public event Action<UnitModel, float, DamageType>? TookDamage;

    /// <summary>添加 Buff 事件。</summary>
    public event Action<UnitModel, IBuff>? BuffAdded;

    /// <summary>移除 Buff 事件。</summary>
    public event Action<UnitModel, IBuff>? BuffRemoved;

    /// <summary>位置变化事件。</summary>
    public event Action? PositionChanged;

    /// <summary>
    /// 设置单位位置；位置变化时打断当前施法并触发 <see cref="PositionChanged"/>。
    /// </summary>
    /// <param name="position">新的世界坐标。</param>
    public void SetPosition(Vector3 position) {
        if (_position != position) {
            _position = position;
            SpellNewSkill(null);
            PositionChanged?.Invoke();
        }
    }

    void IUnitState.CopyStatsFrom(IUnitState source) {
        MaxHealth = source.MaxHealth;
        Health = source.Health;
        CureIntensity = source.CureIntensity;
        PhysicalAttackBase = source.PhysicalAttackBase;
        PhysicalTakePercent = source.PhysicalTakePercent;
        MagicAttackBase = source.MagicAttackBase;
        MagicTakePercent = source.MagicTakePercent;
        BaseSpeed = source.MoveSpeed;
        BodyRadius = source.BodyRadius;
    }

    /// <summary>
    /// 发起新技能施放：打断当前正在施放的技能，并驱动读条推进。
    /// 传入 null 时仅中断当前施法。
    /// </summary>
    /// <param name="skill">要施放的技能，为 null 时仅打断当前施法。</param>
    public void SpellNewSkill(IUnitSkill? skill) {
        SpellingSkill?.SpellBroked();
        SpellingSkill = skill;
        CallSpellingSkill();
    }

    void IUnitState.SpellNewSkill(IUnitSkill skill) {
        SpellNewSkill(skill);
    }

    /// <summary>
    /// 按帧推进技能状态：递减 GCD、更新全部技能计时并驱动当前施法结算。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void UpdateSkillState(double deltaTime) {
        GcdTime -= (float)deltaTime;

        foreach (var skill in SkillsList) {
            skill.UpdateSkill(deltaTime);
        }

        CallSpellingSkill();
    }

    /// <summary>
    /// 驱动当前施法结算：读条完成且不在冷却时触发 GCD 并结束施法。
    /// </summary>
    private void CallSpellingSkill() {
        if (SpellingSkill == null)
            return;

        if (SpellingSkill.CallSkillSpelling()) {
            GcdTime = SpellingSkill.GCDTime;
            SpellNewSkill(null);
        }
    }

    /// <summary>
    /// 按帧更新全部 Buff，并移除已失效的 Buff。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void UpdateBuffList(double deltaTime) {
        var alive = new List<IBuff>();
        foreach (var buff in BuffList) {
            buff.Update(deltaTime, this);
            if (buff.IsAlive) {
                alive.Add(buff);
            }
            else {
                BuffRemoved?.Invoke(this, buff);
            }
        }

        BuffList = alive;
    }

    /// <summary>
    /// 添加一个 Buff；同类型已存在时触发叠加。
    /// </summary>
    /// <param name="buff">要添加的 Buff。</param>
    public void AddBuff(IBuff buff) {
        var existing = BuffList.Find(b => b.BuffName == buff.BuffName);
        if (existing != null) {
            existing.AddSuperpositions(buff);
        }
        else {
            BuffList.Add(buff);
            BuffAdded?.Invoke(this, buff);
        }
    }

    /// <summary>
    /// 移除所有同名的 Buff。
    /// </summary>
    /// <param name="buff">用于确定 Buff 名称的实例。</param>
    public void RemoveBuff(IBuff buff) {
        BuffList.RemoveAll(b => b.BuffName == buff.BuffName);
        BuffRemoved?.Invoke(this, buff);
    }

    /// <summary>
    /// 结算一次伤害，按伤害类型应用对应承受系数。
    /// </summary>
    /// <param name="damageAmount">原始伤害量。</param>
    /// <param name="damageType">伤害类型（物理/魔法）。</param>
    /// <returns>实际扣除的生命值。</returns>
    public float TakeDamage(float damageAmount, DamageType damageType) {
        float damageFixed = damageType switch {
            DamageType.Physical => damageAmount * PhysicalTakePercent,
            DamageType.Magic => damageAmount * MagicTakePercent,
            _ => throw new ArgumentOutOfRangeException(
                nameof(damageType), damageType,
                $"Unknown damage type: {damageType}. UnitId={UnitStateName}, rawDamage={damageAmount}.")
        };

        _health -= damageFixed;
        _health = System.Math.Clamp(_health, 0f, MaxHealth);

        TookDamage?.Invoke(this, damageFixed, damageType);
        return damageFixed;
    }

    /// <summary>
    /// 计算物理伤害的实际数值（基础倍率换算）。
    /// </summary>
    /// <param name="physicalDamage">原始物理伤害量。</param>
    /// <returns>物理攻击加成后的伤害数值。</returns>
    public float PhysicalDamageAmount(float physicalDamage) {
        return physicalDamage * PhysicalAttackBase;
    }

    /// <summary>
    /// 计算魔法伤害的实际数值（基础倍率换算）。
    /// </summary>
    /// <param name="magicDamage">原始魔法伤害量。</param>
    /// <returns>魔法攻击加成后的伤害数值。</returns>
    public float MagicDamageAmount(float magicDamage) {
        return magicDamage * MagicAttackBase;
    }

    /// <summary>
    /// 计算治疗量（治疗强度换算）。
    /// </summary>
    /// <param name="curePotency">原始治疗量。</param>
    /// <returns>治疗强度加成后的治疗数值。</returns>
    public float CureAmount(float curePotency) {
        return CureIntensity * curePotency;
    }

    /// <summary>
    /// 恢复生命值，不超过最大生命值。
    /// </summary>
    /// <param name="health">期望恢复量。</param>
    /// <returns>实际恢复的生命值。</returns>
    public float RestoreHealth(float health) {
        float healthFixed = System.Math.Clamp(_health + health, 0f, MaxHealth) - _health;
        _health += healthFixed;

        return healthFixed;
    }

    /// <summary>
    /// 获取仇恨值最高的单位名称。
    /// </summary>
    /// <returns>仇恨最高的单位名；无仇恨目标时返回 null。</returns>
    public string? GetMaxHateUnitName() {
        float maxHate = 0;
        string? maxHateUnitName = null;
        foreach (var item in Hates) {
            if (item.Value > maxHate) {
                maxHate = item.Value;
                maxHateUnitName = item.Key;
            }
        }

        return maxHateUnitName;
    }
}
