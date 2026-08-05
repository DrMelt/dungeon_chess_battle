using System;
using System.Collections.Generic;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// Godot 技能基类资源，桥接 SkillConfig 配置与运行时 SkillModel 逻辑。
/// </summary>
[GlobalClass]
public partial class UnitSkillBaseGodot : Resource, IUnitSkill {
    /// <summary>运行时技能数据模型，懒加载创建。</summary>
    protected SkillModel? _model = null;

    /// <summary>
    /// 子类重写此属性，直接返回 GameConfigDB 中的 SkillConfig（类型安全，编译期检查）
    /// </summary>
    protected virtual SkillConfig? Config => null;

    /// <summary>
    /// 内部访问 Config，供 SkillResourceTable 等程序集内部使用。
    /// </summary>
    internal SkillConfig? InternalConfig => Config;

    /// <summary>技能图标。</summary>
    [field: Export]
    public Texture2D? Icon { get; private set; } = null;

    /// <summary>已施放时间（编辑器调试用）。</summary>
    [field: ExportGroup("Runtime Parameters")]
    [field: Export]
    public float SkillSpelledTime { get => _model?.SkillSpelledTime ?? field; private set; } = 0;

    /// <summary>冷却经过时间。</summary>
    [field: Export]
    public float SkillCoolingTime { get => _model?.SkillCoolingTime ?? field; private set; } = 0;

    /// <summary>技能目标位置。</summary>
    [Export]
    private Vector3 _targetPos;
    System.Numerics.Vector3 IUnitSkill.TargetPos => new(_targetPos.X, _targetPos.Y, _targetPos.Z);
    /// <summary>技能目标位置。</summary>
    public System.Numerics.Vector3 TargetPos => new(_targetPos.X, _targetPos.Y, _targetPos.Z);

    /// <summary>技能名称。</summary>
    [field: Export]
    public string SkillName { get; private set; } = "";
    /// <summary>技能描述（支持多行文本）。</summary>
    [field: Export(PropertyHint.MultilineText)]
    public string SkillDescription { get; private set; } = "";
    /// <summary>技能施放总时长（秒）。</summary>
    public float SkillSpellTime => _model?.SkillSpellTime ?? 0;
    /// <summary>公共冷却时间（GCD，秒）。</summary>
    public float GCDTime => _model?.GCDTime ?? 0;
    /// <summary>是否需要指定单位目标。</summary>
    public bool NeedUnitTarget => _model?.NeedUnitTarget ?? false;
    /// <summary>是否需要指定位置目标。</summary>
    public bool NeedPosTarget => _model?.NeedPosTarget ?? false;
    /// <summary>技能可附加目标类型。</summary>
    public SkillCanAdd SkillCanAdd => _model?.SkillCanAdd ?? SkillCanAdd.None;
    /// <summary>当前施放进度（0~1）。</summary>
    public float SkillSpellProgress => _model?.SkillSpellProgress ?? 0;
    /// <summary>技能调用单位对象。</summary>
    public IUnitState CallSkillObject => _model?.CallSkillObject ?? throw new InvalidOperationException("Skill model has not been initialized.");

    /// <summary>
    /// 确保运行时模型已创建；未创建时依据 Config 懒加载生成。
    /// </summary>
    private void EnsureModelCreated() {
        if (_model != null)
            return;

        var config = Config ?? throw new InvalidOperationException(
                $"Skill '{GetType().Name}' must override the Config property to provide a valid SkillConfig. " +
                "Config returned null, which means this skill has no configuration.");

        _model = GameConfigDB.ToSkillModel(config);
    }

    /// <summary>
    /// 按帧更新技能施放与冷却计时。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public void UpdateSkill(double delta) {
        EnsureModelCreated();
        _model?.UpdateSkill(delta);
    }

    /// <summary>
    /// 技能是否处于冷却中。
    /// </summary>
    /// <returns>冷却中返回 true。</returns>
    public bool IsCoolingdown() {
        EnsureModelCreated();
        return _model?.IsCoolingdown() ?? true;
    }

    /// <summary>
    /// 设置技能调用者、目标与测试对象。
    /// </summary>
    /// <param name="callSkillObject">技能调用者。</param>
    /// <param name="targetObject">技能目标单位。</param>
    /// <param name="targetPos">技能目标位置。</param>
    /// <param name="testObjects">用于命中测试的候选对象集合。</param>
    public void SetSkill(IUnitState callSkillObject, IUnitState? targetObject, System.Numerics.Vector3? targetPos, IEnumerable<IUnitState> testObjects) {
        EnsureModelCreated();
        if (_model == null)
            return;

        _model.SetSkill(callSkillObject, targetObject, targetPos, testObjects);

        if (targetPos.HasValue) {
            var v = targetPos.Value;
            _targetPos = new Vector3(v.X, v.Y, v.Z);
        }
    }

    /// <summary>
    /// 中断当前读条施法。
    /// </summary>
    public void SpellBroked() {
        EnsureModelCreated();
        _model?.SpellBroked();
    }

    /// <summary>
    /// 执行一次施法判定（吟唱完成时释放技能）。
    /// </summary>
    /// <returns>是否成功释放。</returns>
    public bool CallSkillSpelling() {
        EnsureModelCreated();
        return _model?.CallSkillSpelling() ?? false;
    }

    /// <summary>
    /// 获取技能范围判定器（仅范围伤害类技能有）。
    /// </summary>
    /// <returns>范围判定器，无则返回 null。</returns>
    public IRangeChecker? GetRangeRes() {
        EnsureModelCreated();
        return (_model as SkillRangeDamageModel)?.RangeRes;
    }

    /// <summary>
    /// 技能读条完成后的回调钩子，子类可重写实现具体释放逻辑。
    /// </summary>
    protected virtual void CallSpelledSkill() {
        GD.Print($"{SkillName} is called");
    }
}

/// <summary>
/// 占位 Model，当 Config 为 null 时使用（子类未重写配置）。
/// CallSkillSpelling 在 SkillModel 中已有默认实现，无需额外重写。
/// </summary>
internal class InternalSkillPlaceholder : SkillModel {
}
