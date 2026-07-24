using System.Collections.Generic;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class UnitSkillBaseGodot : Resource, IUnitSkill {
    protected SkillModel _model = null!;

    [Export]
    string skillName = "UnitSkillBaseName";
    public string SkillName => _model?.SkillName ?? skillName;

    [Export]
    Texture2D icon;
    public Texture2D Icon => icon;
    string IUnitSkill.IconPath => icon?.ResourcePath ?? "";

    [Export(PropertyHint.MultilineText)]
    string skillDescription = "UnitSkillBaseDescription";
    public string SkillDescription => _model?.SkillDescription ?? skillDescription;

    [Export]
    float skillNeedSpellTime = 2.0f;
    public float SkillSpellTime => _model?.SkillSpellTime ?? skillNeedSpellTime;

    [Export]
    float skillCooldownTime = 3.0f;

    [Export]
    float gcdTime = 3.0f;
    public float GCDTime => _model?.GCDTime ?? gcdTime;

    [Export]
    bool needUnitTarget = false;
    public bool NeedUnitTarget => _model?.NeedUnitTarget ?? needUnitTarget;

    [Export]
    bool needPosTarget = false;
    public bool NeedPosTarget => _model?.NeedPosTarget ?? needPosTarget;

    [Export]
    EnumSkillCanAdd skillCanAdd = EnumSkillCanAdd.None;
    public EnumSkillCanAdd SkillCanAdd => _model?.SkillCanAdd ?? skillCanAdd;

    [ExportGroup("Runtime Parameters")]
    [Export]
    float skillSpelledTime = 0;
    public float SkillSpelledTime => _model?.SkillSpelledTime ?? skillSpelledTime;

    [Export]
    float skillCoolingTime = 0;
    public float SkillCoolingTime => _model?.SkillCoolingTime ?? skillCoolingTime;

    public float SkillSpellProgress => _model?.SkillSpellProgress ?? 0;

    public IUnitState CallSkillObject => _model?.CallSkillObject!;

    [Export]
    Vector3 _targetPos;
    System.Numerics.Vector3 IUnitSkill.TargetPos => new(_targetPos.X, _targetPos.Y, _targetPos.Z);
    public System.Numerics.Vector3 TargetPos => new(_targetPos.X, _targetPos.Y, _targetPos.Z);

    private void EnsureModelCreated() {
        if (_model != null)
            return;

        _model = CreateModel();

        _model.SkillName = skillName;
        _model.SkillDescription = skillDescription;
        _model.IconPath = icon?.ResourcePath ?? "";
        _model.SkillSpellTime = skillNeedSpellTime;
        _model.SkillCooldownTime = skillCooldownTime;
        _model.GCDTime = gcdTime;
        _model.NeedUnitTarget = needUnitTarget;
        _model.NeedPosTarget = needPosTarget;
        _model.SkillCanAdd = skillCanAdd;
        _model.SkillSpelledTime = skillSpelledTime;
        _model.SkillCoolingTime = skillCoolingTime;
    }

    protected virtual SkillModel CreateModel() {
        return new SkillDummyModel();
    }

    public void UpdateSkill(double delta) {
        EnsureModelCreated();
        _model.UpdateSkill(delta);
    }

    public bool IsCoolingdown() {
        EnsureModelCreated();
        return _model.IsCoolingdown();
    }

    public void SetSkill(IUnitState callSkillObject, IUnitState targetObject, System.Numerics.Vector3? targetPos, IEnumerable<IUnitState> testObjects) {
        EnsureModelCreated();
        _model.SetSkill(callSkillObject, targetObject, targetPos, testObjects);

        if (targetPos.HasValue) {
            var v = targetPos.Value;
            _targetPos = new Vector3(v.X, v.Y, v.Z);
        }
    }

    public void SpellBroked() {
        EnsureModelCreated();
        _model.SpellBroked();
    }

    public bool CallSkillSpelling() {
        EnsureModelCreated();
        return _model.CallSkillSpelling();
    }

    protected virtual void CallSpelledSkill() {
        GD.Print($"{skillName} is called");
    }
}

/// <summary>
/// 无实际操作技能的占位 Model，供不需要实际逻辑的技能使用。
/// </summary>
internal class SkillDummyModel : SkillModel {
    protected override void CallSpelledSkill() {
    }
}
