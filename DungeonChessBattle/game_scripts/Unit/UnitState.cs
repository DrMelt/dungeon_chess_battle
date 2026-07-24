using System;
using System.Collections.Generic;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class UnitState : Resource, IUnitState {
    private readonly UnitModel _model = new();
    private bool _modelSynced;

    #region Export

    [Export]
    string _UnitStateName = "UnitStateName";
    public string UnitStateName {
        get => EnsureSynced().UnitStateName;
        set => _model.UnitStateName = value;
    }

    [Export]
    Godot.Collections.Array<UnitSkillBaseGodot> _skillsList;
    public Godot.Collections.Array<UnitSkillBaseGodot> SkillsList => _skillsList;

    [Export]
    float bodyRadius = 1.0f;
    public float BodyRadius {
        get => EnsureSynced().BodyRadius;
        set => _model.BodyRadius = value;
    }

    [Export]
    EnumCamp _camp = EnumCamp.None;
    public EnumCamp Camp {
        get => EnsureSynced().Camp;
        set => _model.Camp = value;
    }

    [Export]
    float _maxHealth = 1000;
    public float MaxHealth {
        get => EnsureSynced().MaxHealth;
        private set => _model.MaxHealth = value;
    }

    [Export]
    float _health = 1000;
    public float Health {
        get => EnsureSynced().Health;
        private set => _model.Health = value;
    }

    public static float Shield => 0.0f;

    public float Health_Shield => _model.HealthShield;
    public float Health_Percent => _model.HealthPercent;
    public float Health_Shield_Percent => _model.HealthShieldPercent;

    [Export]
    float _cureIntensity = 1.0f;
    public float CureIntensity {
        get => EnsureSynced().CureIntensity;
        set => _model.CureIntensity = value;
    }

    [Export]
    float _physicalAttackBase = 1.0f;
    public float PhysicalAttackBase {
        get => EnsureSynced().PhysicalAttackBase;
        set => _model.PhysicalAttackBase = value;
    }

    [Export]
    float _physicalTakePercent = 1.0f;
    public float PhysicalTakePercent {
        get => EnsureSynced().PhysicalTakePercent;
        set => _model.PhysicalTakePercent = value;
    }

    [Export]
    float _magicAttackBase = 1.0f;
    public float MagicAttackBase {
        get => EnsureSynced().MagicAttackBase;
        set => _model.MagicAttackBase = value;
    }

    [Export]
    float _magicTakePercent = 1.0f;
    public float MagicTakePercent {
        get => EnsureSynced().MagicTakePercent;
        set => _model.MagicTakePercent = value;
    }

    [Export]
    float _baseSpeed = 2.0f;
    public float BaseSpeed {
        get => EnsureSynced().BaseSpeed;
        set => _model.BaseSpeed = value;
    }
    public float MoveSpeed => _model.MoveSpeed;

    [ExportGroup("Runtime Parameters")]
    [Export]
    Vector3 _position = Vector3.Zero;
    public Vector3 Position => _position;
    System.Numerics.Vector3 IUnitState.Position => new(_position.X, _position.Y, _position.Z);
    public void SetGlobalPosition(Vector3 position) {
        if (_position != position) {
            _position = position;
            _model.SetPosition(new System.Numerics.Vector3(position.X, position.Y, position.Z));
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
    UnitSkillBaseGodot spellingSkill;
    public UnitSkillBaseGodot SpellingSkill => spellingSkill;

    [Export]
    float gcdTime;

    [ExportSubgroup("Hate")]
    [Export]
    Godot.Collections.Dictionary<string, float> _hates;
    #endregion

    #region Events

    public Action<float> OnHealthChangedEnvent;
    public Action<float> OnMaxHealthChangedEnvent;
    public Action<float> OnShieldChangedEnvent;
    public Action<UnitState, float, Enum_DamageType> OnTookDamageEvent;
    public Action<UnitState, BuffBaseGodot> OnBuffAddedEvent;
    public Action<UnitState, BuffBaseGodot> OnBuffRemovedEvent;

    #endregion

    private UnitModel EnsureSynced() {
        if (!_modelSynced) {
            _model.UnitStateName = _UnitStateName;
            _model.BodyRadius = bodyRadius;
            _model.Camp = _camp;
            _model.MaxHealth = _maxHealth;
            _model.Health = _health;
            _model.CureIntensity = _cureIntensity;
            _model.PhysicalAttackBase = _physicalAttackBase;
            _model.PhysicalTakePercent = _physicalTakePercent;
            _model.MagicAttackBase = _magicAttackBase;
            _model.MagicTakePercent = _magicTakePercent;
            _model.BaseSpeed = _baseSpeed;
            _model.Hates = new Dictionary<string, float>(_hates ?? []);
            _model.HealthChanged += OnModelHealthChanged;
            _model.MaxHealthChanged += OnModelMaxHealthChanged;
            _model.ShieldChanged += OnModelShieldChanged;
            _model.TookDamage += OnModelTookDamage;
            _modelSynced = true;
        }

        return _model;
    }

    private void OnModelHealthChanged(float health) => OnHealthChangedEnvent?.Invoke(health);
    private void OnModelMaxHealthChanged(float maxHealth) => OnMaxHealthChangedEnvent?.Invoke(maxHealth);
    private void OnModelShieldChanged(float shield) => OnShieldChangedEnvent?.Invoke(shield);
    private void OnModelTookDamage(UnitModel model, float damage, Enum_DamageType type) => OnTookDamageEvent?.Invoke(this, damage, type);

    public void InvokeEnvents() {
        OnHealthChangedEnvent?.Invoke(Health);
        OnMaxHealthChangedEnvent?.Invoke(MaxHealth);
        OnShieldChangedEnvent?.Invoke(Shield);
        // TODO: invoke other events
    }

    #region Skill

    void UpdateSkillState(double deltaTime) {
        gcdTime -= (float)deltaTime;
        EnsureSynced();

        foreach (var skill in SkillsList) {
            skill.UpdateSkill(deltaTime);
        }

        CallSpellingSkill();
    }

    public void SpellNewSkill(IUnitSkill unitSkillBase) {
        spellingSkill?.SpellBroked();

        spellingSkill = unitSkillBase as UnitSkillBaseGodot;
        CallSpellingSkill();
    }
    void IUnitState.SpellNewSkill(IUnitSkill unitSkillBase) {
        SpellNewSkill(unitSkillBase);
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
        return _model.GetMaxHateUnitName() ?? "";
    }
    #endregion

    #region BUFF

    List<BuffBaseGodot> buffList = [];
    public List<BuffBaseGodot> BuffList => buffList;

    public void AddBuff(IBuff buff) {
        if (buff is not BuffBaseGodot godotBuff)
            return;

        _model.AddBuff(godotBuff);

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
        if (buff is not BuffBaseGodot godotBuff)
            return;

        _model.RemoveBuff(godotBuff);
        buffList.RemoveAll(b => b.buffName == godotBuff.buffName);
        OnBuffRemovedEvent?.Invoke(this, godotBuff);
    }

    void UpdateBuffList(double deltaTime) {
        _model.UpdateBuffList(deltaTime);

        List<BuffBaseGodot> tempList = [];
        foreach (BuffBaseGodot buffBase in buffList) {
            if (buffBase.IsAlive) {
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

    public float TakeDamage(float damageAmount, Enum_DamageType damageType) {
        return EnsureSynced().TakeDamage(damageAmount, damageType);
    }

    public float PhysicalDamageAmount(float physicalDamage) {
        return EnsureSynced().PhysicalDamageAmount(physicalDamage);
    }

    public float MagicDamageAmount(float magicDamage) {
        return EnsureSynced().MagicDamageAmount(magicDamage);
    }

    #endregion

    public float CureAmount(float curePotency) {
        return EnsureSynced().CureAmount(curePotency);
    }

    public float RestoreHealth(float health) {
        return EnsureSynced().RestoreHealth(health);
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
