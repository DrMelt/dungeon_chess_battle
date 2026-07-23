using Godot;

using DungeonChessBattle.Core;

namespace DungeonChessBattle;

public partial class EffectHints : Node {

    [Export]
    UserInterfaceRes userInterfaceRes;


    [Export]
    UnitsInScene_Show unitsInScene_Show_Ref;
    [Export]
    Node2d_UserUI userUI_Ref;

    [ExportGroup("Effect Range Hints")]
    [Export]
    PackedScene _effectCircleRange_PKS;
    SkillRangeCircular_Hint NewEffectCircularRange {
        get => _effectCircleRange_PKS.Instantiate<SkillRangeCircular_Hint>();
    }
    [Export]
    PackedScene _effectRectRange_PKS;
    SkillRangeRect_Hint NewEffectRectRange {
        get => _effectRectRange_PKS.Instantiate<SkillRangeRect_Hint>();
    }

    void ShowSkill_Range(Skill_Range_Damage skill_Range_Damage, Vector3 fromPos, Vector3 toPos) {
        // The range hint of skills
        if (skill_Range_Damage.Skill_Range_Res_Base is DungeonChessBattle.Core.Range.CircularRangeResGodot circularRange_Res) {
            SkillRangeCircular_Hint effectCircleRange = NewEffectCircularRange;

            AddChild(effectCircleRange);
            effectCircleRange.Init(fromPos, toPos, circularRange_Res.NearClamp, circularRange_Res.FarClamp, circularRange_Res.RadianFrom, circularRange_Res.RadianTo);
            effectCircleRange.SetSkillProgress(skill_Range_Damage.SkillSpellProgress);
        }
        else if (skill_Range_Damage.Skill_Range_Res_Base is DungeonChessBattle.Core.Range.RectRangeResGodot rectRange_Res) {
            SkillRangeRect_Hint effectRectRange = NewEffectRectRange;

            AddChild(effectRectRange);
            effectRectRange.Init(fromPos, toPos, rectRange_Res.NearClamp, rectRange_Res.FarClamp, rectRange_Res.FromL, rectRange_Res.ToR);
            effectRectRange.SetSkillProgress(skill_Range_Damage.SkillSpellProgress);
        }
    }

    public override void _Process(double delta) {
        var children = GetChildren();
        foreach (var child in children) {
            child.QueueFree();
        }


        var waitingList = userUI_Ref.WaitingTargetSkillList();
        foreach (ButtonSkillBase buttonSkill in waitingList) {
            // Range skill hint with mouse position
            if (buttonSkill.BindSkill is Skill_Range_Damage skill_Range_Damage) {
                if (userInterfaceRes.MouseGoundPosition.HasValue) {
                    ShowSkill_Range(skill_Range_Damage, buttonSkill.BindUnitState.Position, userInterfaceRes.MouseGoundPosition.Value);
                }
            }
        }

        var units = unitsInScene_Show_Ref.UnitsArr;
        foreach (UnitState unitState in units) {
            if (unitState.SpellingSkill != null) {
                if (unitState.SpellingSkill is Skill_Range_Damage skill_Range_Damage) {
                    ShowSkill_Range(skill_Range_Damage, unitState.Position, new Godot.Vector3(skill_Range_Damage.TargetPos.X, skill_Range_Damage.TargetPos.Y, skill_Range_Damage.TargetPos.Z));
                }
            }
        }

    }
}
