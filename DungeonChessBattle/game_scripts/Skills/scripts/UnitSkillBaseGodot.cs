using DungeonChessBattle.Core.Interfaces;
using Godot;
using System.Collections.Generic;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class UnitSkillBaseGodot : Resource, IUnitSkill {
    [Export]
    string skillName = "UnitSkillBaseName";
    public string SkillName => skillName;

    [Export]
    Texture2D icon;
    public Texture2D Icon => icon;
    string IUnitSkill.IconPath => icon?.ResourcePath ?? "";

    [Export(PropertyHint.MultilineText)]
    string skillDescription = "UnitSkillBaseDescription";
    public string SkillDescription => skillDescription;

    [Export]
    float skillNeedSpellTime = 2.0f;
    public float SkillSpellTime => skillNeedSpellTime;

    [Export]
    float skillCooldownTime = 3.0f;

    [Export]
    float gcdTime = 3.0f;
    public float GCDTime => gcdTime;

    [Export]
    bool needUnitTarget = false;
    public bool NeedUnitTarget => needUnitTarget;

    [Export]
    bool needPosTarget = false;
    public bool NeedPosTarget => needPosTarget;

    [Export]
    EnumSkillCanAdd skillCanAdd = EnumSkillCanAdd.None;
    public EnumSkillCanAdd SkillCanAdd => skillCanAdd;

    [ExportGroup("Runtime Parameters")]
    [Export]
    float skillSpelledTime = 0;
    public float SkillSpelledTime => skillSpelledTime;

    [Export]
    float skillCoolingTime = 0;
    public float SkillCoolingTime => skillCoolingTime;

    public float SkillSpellProgress => skillSpelledTime / skillNeedSpellTime;

    protected IUnitState _callSkillObject;
    public IUnitState CallSkillObject => _callSkillObject;

    protected IUnitState _targetObject;
    protected IUnitState TargetObject => _targetObject;

    [Export]
    Godot.Vector3 _targetPos;
    System.Numerics.Vector3 IUnitSkill.TargetPos => new(_targetPos.X, _targetPos.Y, _targetPos.Z);
    public System.Numerics.Vector3 TargetPos => new(_targetPos.X, _targetPos.Y, _targetPos.Z);

    protected List<IUnitState> _testObjects = [];
    protected IEnumerable<IUnitState> TestObjects => _testObjects;

    protected void Reset_SpelledSkill() {
        skillCoolingTime = skillCooldownTime;
        skillSpelledTime = 0;
    }

    public void UpdateSkill(double delta) {
        skillCoolingTime -= (float)delta;
        skillSpelledTime += (float)delta;
    }

    public bool IsCoolingdown() {
        return skillCoolingTime > 0;
    }

    static EnumSkillCanAdd SkillAddEnum(IUnitState callSkillObject, IUnitState testObject) {
        EnumSkillCanAdd addEnum = EnumSkillCanAdd.None;
        if (callSkillObject.Camp == testObject.Camp) {
            addEnum |= EnumSkillCanAdd.Same;
        }
        if (callSkillObject.Camp != testObject.Camp) {
            addEnum |= EnumSkillCanAdd.Different;
        }

        return addEnum;
    }

    public void SetSkill(IUnitState callSkillObject, IUnitState targetObject, System.Numerics.Vector3? targetPos, IEnumerable<IUnitState> testObjects) {
        if (needUnitTarget) {
            if (targetObject == null) {
                GD.Print($"{skillName} need a target");
                return;
            }
            if (!SkillAddEnum(callSkillObject, targetObject).HasFlag(skillCanAdd)) {
                GD.Print($"{skillName} can't add to {targetObject.UnitStateName}");
                return;
            }
        }

        skillSpelledTime = 0;

        _callSkillObject = callSkillObject;
        _targetObject = targetObject;
        if (targetPos.HasValue) {
            var v = targetPos.Value;
            _targetPos = new Godot.Vector3(v.X, v.Y, v.Z);
        }
        _testObjects = [.. testObjects];

        callSkillObject.SpellNewSkill(this);
    }

    public void SpellBroked() {
        skillSpelledTime = 0;
    }

    public bool CallSkillSpelling() {
        if (!IsCoolingdown() &&
            skillSpelledTime >= skillNeedSpellTime) {
            CallSpelledSkill();
            Reset_SpelledSkill();
            return true;
        }
        return false;
    }

    protected virtual void CallSpelledSkill() {
        GD.Print($"{skillName} is called to {_targetObject}");
        Reset_SpelledSkill();
    }
}
