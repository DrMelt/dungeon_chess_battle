using System.Collections.Generic;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class UnitSkillBaseGodot : Resource, IUnitSkill {
    protected SkillModel? _model = null;

    /// <summary>
    /// 指向 GameConfigDB 的配置 ID，数值全部从 C# 配置读取
    /// </summary>
    [Export]
    string skillConfigId = "";

    [Export]
    Texture2D icon = null!;
    public Texture2D Icon => icon;
    string IUnitSkill.IconPath => icon?.ResourcePath ?? "";

    [ExportGroup("Runtime Parameters")]
    [Export]
    float skillSpelledTime = 0;
    public float SkillSpelledTime => _model?.SkillSpelledTime ?? skillSpelledTime;

    [Export]
    float skillCoolingTime = 0;
    public float SkillCoolingTime => _model?.SkillCoolingTime ?? skillCoolingTime;

    [Export]
    Vector3 _targetPos;
    System.Numerics.Vector3 IUnitSkill.TargetPos => new(_targetPos.X, _targetPos.Y, _targetPos.Z);
    public System.Numerics.Vector3 TargetPos => new(_targetPos.X, _targetPos.Y, _targetPos.Z);

    public string SkillName => _model?.SkillName ?? "";
    public string SkillDescription => _model?.SkillDescription ?? "";
    public float SkillSpellTime => _model?.SkillSpellTime ?? 0;
    public float GCDTime => _model?.GCDTime ?? 0;
    public bool NeedUnitTarget => _model?.NeedUnitTarget ?? false;
    public bool NeedPosTarget => _model?.NeedPosTarget ?? false;
    public EnumSkillCanAdd SkillCanAdd => _model?.SkillCanAdd ?? EnumSkillCanAdd.None;
    public float SkillSpellProgress => _model?.SkillSpellProgress ?? 0;
    public IUnitState CallSkillObject => _model?.CallSkillObject!;

    private void EnsureModelCreated() {
        if (_model != null)
            return;

        var config = !string.IsNullOrEmpty(skillConfigId) ? GameConfigDB.GetSkill(skillConfigId) : null;
        _model = config != null ? GameConfigDB.ToSkillModel(config) : new InternalSkillPlaceholder();
        _model.IconPath = icon?.ResourcePath ?? "";
    }

    public void UpdateSkill(double delta) {
        EnsureModelCreated();
        _model?.UpdateSkill(delta);
    }

    public bool IsCoolingdown() {
        EnsureModelCreated();
        return _model?.IsCoolingdown() ?? true;
    }

    public void SetSkill(IUnitState callSkillObject, IUnitState targetObject, System.Numerics.Vector3? targetPos, IEnumerable<IUnitState> testObjects) {
        EnsureModelCreated();
        if (_model == null)
            return;

        _model.SetSkill(callSkillObject, targetObject, targetPos, testObjects);

        if (targetPos.HasValue) {
            var v = targetPos.Value;
            _targetPos = new Vector3(v.X, v.Y, v.Z);
        }
    }

    public void SpellBroked() {
        EnsureModelCreated();
        _model?.SpellBroked();
    }

    public bool CallSkillSpelling() {
        EnsureModelCreated();
        return _model?.CallSkillSpelling() ?? false;
    }

    public IRangeRes? GetRangeRes() {
        EnsureModelCreated();
        return (_model as SkillRangeDamageModel)?.RangeRes;
    }

    protected virtual void CallSpelledSkill() {
        GD.Print($"{SkillName} is called");
    }
}

/// <summary>
/// 占位 Model，当 skillConfigId 为空时使用（编辑器未配置的默认行为）。
/// </summary>
internal class InternalSkillPlaceholder : SkillModel {
    protected override void CallSpelledSkill() {
    }
}
