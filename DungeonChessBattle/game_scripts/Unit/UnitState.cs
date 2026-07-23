using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class UnitState : Resource, IUnitState {



    #region Export

    [Export]
    string _UnitStateName = "UnitStateName";
    public string UnitStateName => _UnitStateName;

    [Export]
    Array<UnitSkillBaseGodot> _skillsList;
    public Array<UnitSkillBaseGodot> SkillsList => _skillsList;


    [Export]
    float bodyRadius = 1.0f;
    public float BodyRadius => bodyRadius;

    [Export]
    EnumCamp _camp = EnumCamp.None;
    public EnumCamp Camp => _camp;

    [Export]
    float _maxHealth = 1000;
    public float MaxHealth {
        get => _maxHealth;
        private set {
            if (value != _maxHealth) {
                _maxHealth = value;
                OnMaxHealthChangedEnvent?.Invoke(_maxHealth);
            }
        }
    }
    public Action<float> OnMaxHealthChangedEnvent;

    [Export]
    float _health = 1000;
    public float Health {
        get => _health;
        private set {
            if (value != _health) {
                _health = value;
                OnHealthChangedEnvent?.Invoke(_health);
            }
        }
    }
    public Action<float> OnHealthChangedEnvent;

    public static float Shield {
        get => 0.0f;
    }
    public Action<float> OnShieldChangedEnvent;

    public float Health_Shield => _health + Shield;
    public float Health_Percent => _health / Mathf.Max(MaxHealth, Health_Shield);
    public float Health_Shield_Percent => Health_Shield / MaxHealth;

    [Export]
    float _cureIntensity = 1.0f;
    public float CureIntensity => _cureIntensity;

    [Export]
    float _physicalAttackBase = 1.0f;
    public float PhysicalAttackBase => _physicalAttackBase;

    [Export]
    float _physicalTakePercent = 1.0f;
    public float PhysicalTakePercent => _physicalTakePercent;

    [Export]
    float _magicAttackBase = 1.0f;
    public float MagicAttackBase => _magicAttackBase;

    [Export]
    float _magicTakePercent = 1.0f;
    public float MagicTakePercent => _magicTakePercent;

    [Export]
    float _baseSpeed = 2.0f;
    public float BaseSpeed {
        get => _baseSpeed;
    }
    public float MoveSpeed => BaseSpeed;



    [ExportGroup("Runtime Parameters")]
    [Export]
    Vector3 _position = Vector3.Zero;
    public Vector3 Position => _position;
    System.Numerics.Vector3 IUnitState.Position => new(_position.X, _position.Y, _position.Z);
    public void SetGlobalPosition(Vector3 position) {
        if (_position != position) {
            _position = position;
            UnitMoved();
        }
    }
    private void UnitMoved() {
        SpellNewSkill(null);
    }

    [Export]
    Vector3 _lookAt_Dir = Vector3.Forward;
    public Vector3 LookAt_Dir => _lookAt_Dir;
    public void SetLookAt_Dir(Vector3 lookAt_Dir) {
        lookAt_Dir.Y = 0;
        if (_lookAt_Dir != lookAt_Dir) {
            _lookAt_Dir = lookAt_Dir.Normalized();
        }
    }

    [Export]
    UnitsInScene unitsInSceneRes;
    [Export]
    MotionTimeTable motionTimeTable;

    [ExportSubgroup("Spell")]
    [Export]
    UnitSkillBaseGodot spellingSkill = null;
    public UnitSkillBaseGodot SpellingSkill => spellingSkill;

    public void SpellNewSkill(IUnitSkill unitSkillBase) {
        spellingSkill?.SpellBroked();

        spellingSkill = unitSkillBase as UnitSkillBaseGodot;
        CallSpellingSkill();
    }
    void IUnitState.SpellNewSkill(IUnitSkill unitSkillBase) {
        SpellNewSkill(unitSkillBase);
    }

    [Export]
    float gcdTime = 0.0f;

    [ExportSubgroup("Hate")]
    [Export]
    Godot.Collections.Dictionary<string, float> _hates;
    #endregion


    #region Skill
    void UpdateSkillState(double deltaTime) {
        gcdTime -= (float)deltaTime;

        foreach (var skill in SkillsList) {
            skill.UpdateSkill(deltaTime);
        }

        CallSpellingSkill();
    }

    void CallSpellingSkill() {
        if (spellingSkill == null) {
            return;
        }
        if (spellingSkill.CallSkillSpelling()) {
            gcdTime = spellingSkill.GCDTime;
            SpellNewSkill(null);
        }
    }
    #endregion

    #region Hate
    public string GetMaxHateUnitName() {
        float maxHate = 0;
        string maxHateUnitName = null;
        foreach (KeyValuePair<string, float> item in _hates) {
            if (item.Value > maxHate) {
                maxHate = item.Value;
                maxHateUnitName = item.Key;
            }
        }
        return maxHateUnitName;
    }
    #endregion


    public void InvokeEnvents() {
        OnHealthChangedEnvent?.Invoke(Health);
        OnMaxHealthChangedEnvent?.Invoke(MaxHealth);
        OnShieldChangedEnvent?.Invoke(Shield);
        // TODO: invoke other events
    }

    #region BUFF

    List<BuffBaseGodot> buffList = [];
    public Action<UnitState, BuffBaseGodot> OnBuffAddedEvent;
    public Action<UnitState, BuffBaseGodot> OnBuffRemovedEvent;
    public List<BuffBaseGodot> BuffList {
        get => buffList;
    }

    public void AddBuff(IBuff buff) {
        if (buff is not BuffBaseGodot godotBuff) {
            return;
        }
        BuffBaseGodot find = buffList.Find(b => b.buffName == godotBuff.buffName);
        if (find != null) {
            find.AddSuperpositions(godotBuff);
        }
        else {
            buffList.Add(godotBuff);
            OnBuffAddedEvent?.Invoke(this, godotBuff);
        }
    }

    public void RemoveBuff(IBuff buff) {
        if (buff is not BuffBaseGodot godotBuff) {
            return;
        }
        buffList.RemoveAll(b => b.buffName == godotBuff.buffName);
        OnBuffRemovedEvent?.Invoke(this, godotBuff);
    }

    void UpdateBuffList(double deltaTime) {
        List<BuffBaseGodot> tempList = [];
        foreach (BuffBaseGodot buffBase in buffList) {
            buffBase.Update(deltaTime, this);
            if (buffBase.isAlive) {
                tempList.Add(buffBase);
            }
            else {
                OnBuffRemovedEvent?.Invoke(this, buffBase);
            }
        }

        buffList = tempList;
    }
    #endregion


    #region DAMAGE
    public Action<UnitState, float, Enum_DamageType> OnTookDamageEvent;
    public float TakeDamage(float damageAmount, Enum_DamageType damageType) {
        float damageFixed = 0;
        if (damageType == Enum_DamageType.Physcial) {
            damageFixed = damageAmount * PhysicalTakePercent;
        }
        else if (damageType == Enum_DamageType.Magic) {
            damageFixed = damageAmount * MagicTakePercent;
        }

        // TODO: take into account buffs


        _health -= damageFixed;
        _health = Mathf.Clamp(_health, 0, MaxHealth);

        OnTookDamageEvent?.Invoke(this, damageFixed, damageType);
        return damageFixed;
    }

    public float PhysicalDamageAmount(float physicalDamage) {
        return physicalDamage * PhysicalAttackBase;
    }

    public float MagicDamageAmount(float magicDamage) {
        return magicDamage * MagicAttackBase;
    }

    #endregion

    public float CureAmount(float curePotency) {
        return CureIntensity * curePotency;
    }

    public float RestoreHealth(float health) {
        float healthFixed = Mathf.Clamp(_health + health, 0, MaxHealth) - _health;
        _health += healthFixed;

        return healthFixed;
    }


    #region Update
    internal void UpdateState(double delta) {
        UpdateSkillState(delta);
    }
    internal void UpdateStateInterval(double delta) {
        UpdateBuffList(delta);
    }
    #endregion

}
