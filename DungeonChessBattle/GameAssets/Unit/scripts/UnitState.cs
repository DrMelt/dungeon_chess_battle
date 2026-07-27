using System;
using System.Collections.Generic;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Client;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using DungeonChessBattle.GameAssets.Skills;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class UnitState : Resource, IUnitState {
    private UnitModel? _model;

    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的 UnitConfig（类型安全，编译期检查）
    /// </summary>
    protected virtual UnitConfig? Config => null;

    /// <summary>
    /// 暴露内部 UnitModel，供 BattleService 调用。
    /// </summary>
    public UnitModel Model => EnsureSynced();

    #region Export

    [Export]
    string _UnitStateName = "UnitStateName";
    public string UnitStateName {
        get => EnsureSynced().UnitStateName;
        set => EnsureSynced().UnitStateName = value;
    }

    [Export]
    Godot.Collections.Array<UnitSkillBaseGodot> _skillsList = null!;
    public Godot.Collections.Array<UnitSkillBaseGodot> SkillsList => _skillsList;


    public float BodyRadius => EnsureSynced().BodyRadius;

    [Export]
    EnumCamp _camp = EnumCamp.None;
    public EnumCamp Camp {
        get => EnsureSynced().Camp;
        set => EnsureSynced().Camp = value;
    }

    public float MaxHealth => EnsureSynced().MaxHealth;

    [Export]
    float _health = 1000;
    public float Health {
        get => EnsureSynced().Health;
        set => EnsureSynced().Health = value;
    }

    public static float Shield => 0.0f;

    public float Health_Shield => Model.HealthShield;
    public float Health_Percent => Model.HealthPercent;
    public float Health_Shield_Percent => Model.HealthShieldPercent;

    public float CureIntensity => EnsureSynced().CureIntensity;

    public float PhysicalAttackBase => EnsureSynced().PhysicalAttackBase;

    public float PhysicalTakePercent => EnsureSynced().PhysicalTakePercent;

    public float MagicAttackBase => EnsureSynced().MagicAttackBase;

    public float MagicTakePercent => EnsureSynced().MagicTakePercent;

    public float BaseSpeed => EnsureSynced().BaseSpeed;
    public float MoveSpeed => Model.MoveSpeed;

    [ExportGroup("Runtime Parameters")]
    [Export]
    Vector3 _position = Vector3.Zero;
    public Vector3 Position => _position;
    System.Numerics.Vector3 IUnitState.Position => new(_position.X, _position.Y, _position.Z);
    public void SetGlobalPosition(Vector3 position) {
        if (_position != position) {
            _position = position;
            EnsureSynced().SetPosition(new System.Numerics.Vector3(position.X, position.Y, position.Z));
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
    UnitsInScene unitsInSceneRes = null!;
    [Export]
    MotionTimeTable motionTimeTable = null!;

    [ExportSubgroup("Spell")]
    [Export]
    UnitSkillBaseGodot? spellingSkill;
    public UnitSkillBaseGodot? SpellingSkill => spellingSkill;

    [Export]
    float gcdTime;

    [ExportSubgroup("Hate")]
    [Export]
    Godot.Collections.Dictionary<string, float> _hates = null!;
    #endregion

    #region Events

    public Action<float>? OnHealthChangedEnvent;
    public Action<float>? OnMaxHealthChangedEnvent;
    public Action<UnitState, float, Enum_DamageType>? OnTookDamageEvent;
    public Action<UnitState, BuffBaseGodot>? OnBuffAddedEvent;
    public Action<UnitState, BuffBaseGodot>? OnBuffRemovedEvent;

    #endregion

    private UnitModel EnsureSynced() {
        if (_model != null)
            return _model;

        var config = Config ?? throw new InvalidOperationException(
                $"Unit '{GetType().Name}' must override the Config property to provide a valid UnitConfig. " +
                "Config returned null, which means this unit has no configuration.");
        _model = GameConfigDB.ToUnitModel(config);

        // 从 Config.Skills + 资源表 自动构建 Godot 技能列表（不再依赖 .tres 手动维护 _skillsList）
        if (_skillsList == null || _skillsList.Count == 0) {
            _skillsList = [];
            foreach (var skillConfig in config.Skills) {
                _skillsList.Add(SkillResourceTable.LoadResource(skillConfig));
            }
        }

        _model.UnitStateName = _UnitStateName;
        _model.Camp = _camp;
        _model.Health = _health;
        _model.Hates = new Dictionary<string, float>(_hates ?? []);
        _model.HealthChanged += OnModelHealthChanged;
        _model.MaxHealthChanged += OnModelMaxHealthChanged;
        _model.TookDamage += OnModelTookDamage;

        return _model;
    }

    private void OnModelHealthChanged(float health) => OnHealthChangedEnvent?.Invoke(health);
    private void OnModelMaxHealthChanged(float maxHealth) => OnMaxHealthChangedEnvent?.Invoke(maxHealth);
    private void OnModelTookDamage(UnitModel model, float damage, Enum_DamageType type) => OnTookDamageEvent?.Invoke(this, damage, type);

    public void InvokeEnvents() {
        OnHealthChangedEnvent?.Invoke(Health);
        OnMaxHealthChangedEnvent?.Invoke(MaxHealth);
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

    public void SpellNewSkill(IUnitSkill? unitSkillBase) {
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
        return EnsureSynced().GetMaxHateUnitName() ?? "";
    }
    #endregion

    #region BUFF

    List<BuffBaseGodot> buffList = [];
    public List<BuffBaseGodot> BuffList => buffList;

    public void AddBuff(IBuff buff) {
        if (buff is not BuffBaseGodot godotBuff)
            return;

        EnsureSynced().AddBuff(godotBuff);

        BuffBaseGodot? find = buffList.Find(b => b.BuffName == godotBuff.BuffName);
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

        EnsureSynced().RemoveBuff(godotBuff);
        buffList.RemoveAll(b => b.BuffName == godotBuff.BuffName);
        OnBuffRemovedEvent?.Invoke(this, godotBuff);
    }

    public void UpdateBuffList(double deltaTime) {
        // 通过统一战斗服务更新 Buff（支持本地/网络双模式）
        if (BattleServiceProvider.IsInitialized) {
            BattleServiceProvider.ClientService.UpdateBuffs(null!, [EnsureSynced()], deltaTime);
        }
        else {
            EnsureSynced().UpdateBuffList(deltaTime);
        }

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
