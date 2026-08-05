using System;
using System.Collections.Generic;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using DungeonChessBattle.GameAssets.Skills;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// Godot 单位状态资源，桥接 UnitConfig 配置与运行时 UnitModel 逻辑。
/// 负责属性同步、技能施放、仇恨、Buff、伤害结算与状态更新。
/// </summary>
[GlobalClass]
public partial class UnitState : Resource, IUnitState {
    /// <summary>运行时单位数据模型，懒加载创建。</summary>
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

    /// <summary>单位展示名称。</summary>
    [Export]
    private string _UnitStateName = "UnitStateName";
    /// <summary>单位展示名称（同步自导出字段）。</summary>
    public string UnitStateName {
        get => EnsureSynced().UnitStateName;
        set => EnsureSynced().UnitStateName = value;
    }

    /// <summary>单位技能列表。</summary>
    [Export]
    private Godot.Collections.Array<UnitSkillBaseGodot>? _skillsList;
    /// <summary>单位技能列表（懒加载创建；未配置时从技能配置表自动构建）。</summary>
    public Godot.Collections.Array<UnitSkillBaseGodot> SkillsList => _skillsList ??= [];


    /// <summary>单位身体半径。</summary>
    public float BodyRadius => EnsureSynced().BodyRadius;

    /// <summary>单位所属阵营列表。</summary>
    [Export]
    private string[] _camps = [];
    /// <summary>单位所属阵营列表（同步自导出字段）。</summary>
    public List<string> Camps => EnsureSynced().Camps;

    /// <summary>单位最大生命值。</summary>
    public float MaxHealth => EnsureSynced().MaxHealth;

    /// <summary>单位当前生命值。</summary>
    [Export]
    private float _health = 1000;
    /// <summary>单位当前生命值（同步自导出字段）。</summary>
    public float Health {
        get => EnsureSynced().Health;
        set => EnsureSynced().Health = value;
    }

    /// <summary>单位护盾值（当前为静态占位，恒为 0）。</summary>
    public static float Shield => 0.0f;

    /// <summary>生命值与护盾值总和。</summary>
    public float Health_Shield => Model.HealthShield;
    /// <summary>当前生命值百分比（0~1）。</summary>
    public float Health_Percent => Model.HealthPercent;
    /// <summary>生命值与护盾值总和百分比（0~1）。</summary>
    public float Health_Shield_Percent => Model.HealthShieldPercent;

    /// <summary>治疗强度。</summary>
    public float CureIntensity => EnsureSynced().CureIntensity;

    /// <summary>物理攻击基础值。</summary>
    public float PhysicalAttackBase => EnsureSynced().PhysicalAttackBase;

    /// <summary>受到的物理伤害百分比修正。</summary>
    public float PhysicalTakePercent => EnsureSynced().PhysicalTakePercent;

    /// <summary>魔法攻击基础值。</summary>
    public float MagicAttackBase => EnsureSynced().MagicAttackBase;

    /// <summary>受到的魔法伤害百分比修正。</summary>
    public float MagicTakePercent => EnsureSynced().MagicTakePercent;

    /// <summary>基础移动速度。</summary>
    public float BaseSpeed => EnsureSynced().BaseSpeed;
    /// <summary>当前移动速度。</summary>
    public float MoveSpeed => Model.MoveSpeed;

    /// <summary>单位世界位置。</summary>
    [ExportGroup("Runtime Parameters")]
    [Export]
    private Vector3 _position = Vector3.Zero;
    /// <summary>单位世界位置。</summary>
    public Vector3 Position => _position;
    System.Numerics.Vector3 IUnitState.Position => new(_position.X, _position.Y, _position.Z);
    /// <summary>
    /// 设置单位全局位置；位置变化时同步到运行模型并触发移动事件。
    /// </summary>
    /// <param name="position">新的世界坐标。</param>
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

    /// <summary>单位朝向方向。</summary>
    [Export]
    private Vector3 _lookAt_Dir = Vector3.Forward;
    /// <summary>单位朝向方向。</summary>
    public Vector3 LookAt_Dir => _lookAt_Dir;
    /// <summary>
    /// 设置单位朝向方向；水平化并归一化后存储。
    /// </summary>
    /// <param name="lookAt_Dir">目标朝向方向向量。</param>
    public void SetLookAt_Dir(Vector3 lookAt_Dir) {
        lookAt_Dir.Y = 0;
        if (_lookAt_Dir != lookAt_Dir) {
            _lookAt_Dir = lookAt_Dir.Normalized();
        }
    }

    /// <summary>所属场景单位集合资源。</summary>
    [Export]
    private UnitsInScene? unitsInSceneRes;
    /// <summary>动作时间表资源。</summary>
    [Export]
    private MotionTimeTable? motionTimeTable;

    /// <summary>当前正在读条施放的技能。</summary>
    [field: ExportSubgroup("Spell")]
    [field: Export]
    public UnitSkillBaseGodot? SpellingSkill {
        get; private set;
    }

    /// <summary>公共冷却（GCD）剩余时间。</summary>
    [Export]
    private float gcdTime;

    /// <summary>仇恨值字典（单位名 → 仇恨值）。</summary>
    [ExportSubgroup("Hate")]
    [Export]
    private Godot.Collections.Dictionary<string, float>? _hates;
    #endregion

    #region Events

    /// <summary>生命值变化事件。</summary>
    public Action<float>? OnHealthChangedEvent;
    /// <summary>最大生命值变化事件。</summary>
    public Action<float>? OnMaxHealthChangedEvent;
    /// <summary>受击事件。</summary>
    public Action<UnitState, float, DamageType>? OnTookDamageEvent;
    /// <summary>Buff 添加事件。</summary>
    public Action<UnitState, BuffBaseGodot>? OnBuffAddedEvent;
    /// <summary>Buff 移除事件。</summary>
    public Action<UnitState, BuffBaseGodot>? OnBuffRemovedEvent;

    #endregion

    /// <summary>
    /// 确保运行时模型已创建；未创建时依据 Config 懒加载生成并同步导出字段。
    /// </summary>
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
        _model.Camps = [.. _camps];
        _model.Health = _health;
        if (_hates == null)
            throw new InvalidOperationException(
                $"Unit '{GetType().Name}' has null _hates dictionary. Check the .tres resource.");
        _model.Hates = new Dictionary<string, float>(_hates);
        _model.HealthChanged += OnModelHealthChanged;
        _model.MaxHealthChanged += OnModelMaxHealthChanged;
        _model.TookDamage += OnModelTookDamage;

        return _model;
    }

    /// <summary>模型生命值变化回调。</summary>
    private void OnModelHealthChanged(float health) => OnHealthChangedEvent?.Invoke(health);
    /// <summary>模型最大生命值变化回调。</summary>
    private void OnModelMaxHealthChanged(float maxHealth) => OnMaxHealthChangedEvent?.Invoke(maxHealth);
    /// <summary>模型受击回调。</summary>
    private void OnModelTookDamage(UnitModel model, float damage, DamageType type) => OnTookDamageEvent?.Invoke(this, damage, type);

    /// <summary>
    /// 手动触发生命周期事件通知。
    /// </summary>
    public void InvokeEvents() {
        OnHealthChangedEvent?.Invoke(Health);
        OnMaxHealthChangedEvent?.Invoke(MaxHealth);
        // TODO: invoke other events
    }

    #region Skill

    /// <summary>
    /// 更新技能冷却并驱动当前读条技能。
    /// </summary>
    /// <param name="deltaTime">距上一帧的秒数。</param>
    private void UpdateSkillState(double deltaTime) {
        gcdTime -= (float)deltaTime;
        EnsureSynced();

        foreach (var skill in SkillsList) {
            skill.UpdateSkill(deltaTime);
        }

        CallSpellingSkill();
    }

    /// <summary>
    /// 开始施放新技能：中断当前读条技能并替换为新技能。
    /// </summary>
    /// <param name="unitSkillBase">要施放的技能。</param>
    public void SpellNewSkill(IUnitSkill? unitSkillBase) {
        SpellingSkill?.SpellBroked();

        SpellingSkill = unitSkillBase as UnitSkillBaseGodot;
        CallSpellingSkill();
    }
    void IUnitState.SpellNewSkill(IUnitSkill unitSkillBase) {
        SpellNewSkill(unitSkillBase);
    }

    /// <summary>
    /// 驱动当前读条技能：吟唱完成时设置 GCD 并结束施放。
    /// </summary>
    private void CallSpellingSkill() {
        if (SpellingSkill == null) {
            return;
        }
        if (SpellingSkill.CallSkillSpelling()) {
            gcdTime = SpellingSkill.GCDTime;
            SpellNewSkill(null);
        }
    }
    #endregion

    #region Hate

    /// <summary>
    /// 获取仇恨值最高的单位名称。
    /// </summary>
    /// <returns>最高仇恨单位名，无则返回 null。</returns>
    public string? GetMaxHateUnitName() {
        return EnsureSynced().GetMaxHateUnitName();
    }
    #endregion

    #region BUFF

    /// <summary>当前生效的 Buff 列表。</summary>
    public List<BuffBaseGodot> BuffList { get; private set; } = [];

    /// <summary>
    /// 添加 Buff：已有同类型则叠加层数，否则新增并触发事件。
    /// </summary>
    /// <param name="buff">要添加的 Buff。</param>
    public void AddBuff(IBuff buff) {
        if (buff is not BuffBaseGodot godotBuff)
            return;

        EnsureSynced().AddBuff(godotBuff);

        BuffBaseGodot? find = BuffList.Find(b => b.BuffName == godotBuff.BuffName);
        if (find != null) {
            find.AddSuperpositions(godotBuff);
        }
        else {
            BuffList.Add(godotBuff);
            OnBuffAddedEvent?.Invoke(this, godotBuff);
        }
    }

    /// <summary>
    /// 移除 Buff 并触发移除事件。
    /// </summary>
    /// <param name="buff">要移除的 Buff。</param>
    public void RemoveBuff(IBuff buff) {
        if (buff is not BuffBaseGodot godotBuff)
            return;

        EnsureSynced().RemoveBuff(godotBuff);
        BuffList.RemoveAll(b => b.BuffName == godotBuff.BuffName);
        OnBuffRemovedEvent?.Invoke(this, godotBuff);
    }

    /// <summary>
    /// 更新所有 Buff 计时；过期 Buff 自动移除并触发事件。
    /// </summary>
    /// <param name="deltaTime">距上次更新的秒数。</param>
    public void UpdateBuffList(double deltaTime) {
        EnsureSynced().UpdateBuffList(deltaTime);

        List<BuffBaseGodot> tempList = [];
        foreach (BuffBaseGodot buffBase in BuffList) {
            if (buffBase.IsAlive) {
                tempList.Add(buffBase);
            }
            else {
                OnBuffRemovedEvent?.Invoke(this, buffBase);
            }
        }

        BuffList = tempList;
    }
    #endregion

    #region DAMAGE

    /// <summary>
    /// 受到伤害结算。
    /// </summary>
    /// <param name="damageAmount">原始伤害值。</param>
    /// <param name="damageType">伤害类型。</param>
    /// <returns>实际受到的伤害值。</returns>
    public float TakeDamage(float damageAmount, DamageType damageType) {
        return EnsureSynced().TakeDamage(damageAmount, damageType);
    }

    /// <summary>
    /// 计算物理伤害实际值（含免伤修正）。
    /// </summary>
    /// <param name="physicalDamage">原始物理伤害。</param>
    /// <returns>修正后的物理伤害。</returns>
    public float PhysicalDamageAmount(float physicalDamage) {
        return EnsureSynced().PhysicalDamageAmount(physicalDamage);
    }

    /// <summary>
    /// 计算魔法伤害实际值（含免伤修正）。
    /// </summary>
    /// <param name="magicDamage">原始魔法伤害。</param>
    /// <returns>修正后的魔法伤害。</returns>
    public float MagicDamageAmount(float magicDamage) {
        return EnsureSynced().MagicDamageAmount(magicDamage);
    }

    #endregion

    /// <summary>
    /// 计算治疗效果实际值。
    /// </summary>
    /// <param name="curePotency">原始治疗量。</param>
    /// <returns>实际治疗量。</returns>
    public float CureAmount(float curePotency) {
        return EnsureSynced().CureAmount(curePotency);
    }

    /// <summary>
    /// 直接恢复生命值。
    /// </summary>
    /// <param name="health">要恢复的生命值。</param>
    /// <returns>实际恢复值。</returns>
    public float RestoreHealth(float health) {
        return EnsureSynced().RestoreHealth(health);
    }

    #region Update

    /// <summary>
    /// 每帧更新单位技能状态。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    internal void UpdateState(double delta) {
        UpdateSkillState(delta);
    }
    /// <summary>
    /// 按间隔更新单位 Buff 状态。
    /// </summary>
    /// <param name="delta">距上次更新的秒数。</param>
    internal void UpdateStateInterval(double delta) {
        UpdateBuffList(delta);
    }
    #endregion

}
