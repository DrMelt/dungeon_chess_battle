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

    public string UnitStateName { get; set; } = "UnitStateName";

    public float BodyRadius { get; set; } = 1.0f;

    public EnumCamp Camp { get; set; } = EnumCamp.None;

    #endregion

    #region Health & Shield

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
    public float Health {
        get => _health;
        set {
            if (System.MathF.Abs(value - _health) > 0.0001f) {
                _health = value;
                HealthChanged?.Invoke(_health);
            }
        }
    }

    public static float Shield => 0.0f;

    public float HealthShield => Health + Shield;

    public float HealthPercent => Health / System.MathF.Max(MaxHealth, HealthShield);

    public float HealthShieldPercent => HealthShield / MaxHealth;

    public float CureIntensity {
        get; set;
    }

    public float PhysicalAttackBase {
        get; set;
    }

    public float PhysicalTakePercent {
        get; set;
    }

    public float MagicAttackBase {
        get; set;
    }

    public float MagicTakePercent {
        get; set;
    }

    public float BaseSpeed {
        get; set;
    }

    public float MoveSpeed => BaseSpeed;

    private Vector3 _position = Vector3.Zero;
    public Vector3 Position => _position;

    public Vector3 LookAtDir { get; set; } = new Vector3(0, 0, 1);

    public float GcdTime {
        get; set;
    }

    public List<IUnitSkill> SkillsList { get; set; } = [];

    public IUnitSkill? SpellingSkill {
        get; private set;
    }

    private List<IBuff> _buffList = [];
    public List<IBuff> BuffList => _buffList;

    public Dictionary<string, float> Hates { get; set; } = [];

    #endregion

    // Events
    public event Action<float>? HealthChanged;
    public event Action<float>? MaxHealthChanged;
    public event Action<UnitModel, float, Enum_DamageType>? TookDamage;
    public event Action<UnitModel, IBuff>? BuffAdded;
    public event Action<UnitModel, IBuff>? BuffRemoved;
    public event Action? PositionChanged;

    public void SetPosition(Vector3 position) {
        if (_position != position) {
            _position = position;
            SpellNewSkill(null);
            PositionChanged?.Invoke();
        }
    }

    public void SpellNewSkill(IUnitSkill? skill) {
        SpellingSkill?.SpellBroked();
        SpellingSkill = skill;
        CallSpellingSkill();
    }

    void IUnitState.SpellNewSkill(IUnitSkill skill) {
        SpellNewSkill(skill);
    }

    public void UpdateSkillState(double deltaTime) {
        GcdTime -= (float)deltaTime;

        foreach (var skill in SkillsList) {
            skill.UpdateSkill(deltaTime);
        }

        CallSpellingSkill();
    }

    private void CallSpellingSkill() {
        if (SpellingSkill == null)
            return;

        if (SpellingSkill.CallSkillSpelling()) {
            GcdTime = SpellingSkill.GCDTime;
            SpellNewSkill(null);
        }
    }

    public void UpdateBuffList(double deltaTime) {
        var alive = new List<IBuff>();
        foreach (var buff in _buffList) {
            buff.Update(deltaTime, this);
            if (buff.IsAlive) {
                alive.Add(buff);
            }
            else {
                BuffRemoved?.Invoke(this, buff);
            }
        }

        _buffList = alive;
    }

    public void AddBuff(IBuff buff) {
        var existing = _buffList.Find(b => b.BuffName == buff.BuffName);
        if (existing != null) {
            existing.AddSuperpositions(buff);
        }
        else {
            _buffList.Add(buff);
            BuffAdded?.Invoke(this, buff);
        }
    }

    public void RemoveBuff(IBuff buff) {
        _buffList.RemoveAll(b => b.BuffName == buff.BuffName);
        BuffRemoved?.Invoke(this, buff);
    }

    public float TakeDamage(float damageAmount, Enum_DamageType damageType) {
        float damageFixed = damageType switch {
            Enum_DamageType.Physcial => damageAmount * PhysicalTakePercent,
            Enum_DamageType.Magic => damageAmount * MagicTakePercent,
            _ => throw new ArgumentOutOfRangeException(
                nameof(damageType), damageType,
                $"Unknown damage type: {damageType}. UnitId={UnitStateName}, rawDamage={damageAmount}.")
        };

        _health -= damageFixed;
        _health = System.Math.Clamp(_health, 0f, MaxHealth);

        TookDamage?.Invoke(this, damageFixed, damageType);
        return damageFixed;
    }

    public float PhysicalDamageAmount(float physicalDamage) {
        return physicalDamage * PhysicalAttackBase;
    }

    public float MagicDamageAmount(float magicDamage) {
        return magicDamage * MagicAttackBase;
    }

    public float CureAmount(float curePotency) {
        return CureIntensity * curePotency;
    }

    public float RestoreHealth(float health) {
        float healthFixed = System.Math.Clamp(_health + health, 0f, MaxHealth) - _health;
        _health += healthFixed;

        return healthFixed;
    }

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
