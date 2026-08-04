using Godot;

namespace DungeonChessBattle;

public partial class StateBarMini : Control {

    public StateBarMiniInterRefs? InterRefs {
        get; private set;
    }

    bool mouseOn = false;

    UnitState? bindingUnitStateRes;

    public override void _Ready() {
        InterRefs = GetNode<StateBarMiniInterRefs>("StateBarMiniInterRefs");
        MouseEntered += () => {
            mouseOn = true;
            if (InterRefs?.OutlineRef != null)
                InterRefs.OutlineRef.Visible = true;
        };
        MouseExited += () => {
            mouseOn = false;
            if (InterRefs?.OutlineRef != null)
                InterRefs.OutlineRef.Visible = false;
        };
    }

    public void BindUnitState(UnitState unitState) {
        bindingUnitStateRes = unitState;
    }

    public override void _Process(double delta) {
        if (InterRefs == null || bindingUnitStateRes == null)
            return;
        InterRefs.ContainerBuffsRef?.UpdateUI_WithUnit(bindingUnitStateRes);
        InterRefs.HpStateBarRef?.UpdateUI_WithUnit(bindingUnitStateRes);
        InterRefs.SkillProgressBarRef?.UpdateUI_WithUnit(bindingUnitStateRes);
    }


}
