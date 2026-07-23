using Godot;
using System.Collections.Generic;

namespace DungeonChessBattle;

public partial class SkillsList : Control {
    [Export]
    UnitsInScene_Show unitsInGameRef;
    public UnitsInScene_Show UnitsInGameRef => unitsInGameRef;

    [ExportGroup("Internal Parameters")]
    [Export]
    PackedScene skillButtonPackedScene;

    [Export]
    HBoxContainer hBoxContainerRef;





    readonly List<ButtonSkillBase> skillButtonList = [];
    internal void UpdateSkillsList(UnitGameShow unitShow) {

        var children = hBoxContainerRef.GetChildren();
        foreach (var child in children) {
            child.QueueFree();
        }
        skillButtonList.Clear();

        if (unitShow == null) {
            return;
        }
        if (unitShow.SkillsList == null) {
            return;
        }

        foreach (DungeonChessBattle.Core.UnitSkillBaseGodot skill in unitShow.SkillsList) {
            ButtonSkillBase buttonSkill = skillButtonPackedScene.Instantiate<ButtonSkillBase>();
            buttonSkill.Init(skill, unitShow.UnitStateRec, this);
            hBoxContainerRef.AddChild(buttonSkill);

            skillButtonList.Add(buttonSkill);
        }

    }

    internal bool IsWaitTarget() {
        foreach (ButtonSkillBase buttonSkill in skillButtonList) {
            if (buttonSkill.WaitTarget) {
                return true;
            }
        }
        return false;
    }

    public List<ButtonSkillBase> WaitingTargetSkillList() {
        List<ButtonSkillBase> waitingTargetSkillList = [];
        foreach (ButtonSkillBase buttonSkill in skillButtonList) {
            if (buttonSkill.WaitTarget) {
                waitingTargetSkillList.Add(buttonSkill);
            }
        }
        return waitingTargetSkillList;
    }
}
