using DungeonChessBattle.Core;
using Godot;

namespace DungeonChessBattle;

public partial class StateBarMini : Control {

    [ExportGroup("Internal Parameters")]
    [ExportSubgroup("Buffs")]
    [Export]
    ContainerBuffs containerBuffsRef = null!;

    [ExportSubgroup("State Bar")]
    [Export]
    Panel outlineRef = null!;

    [Export]
    HP_StateBar hp_StateBarRef = null!;

    [Export]
    SkillProgressBar skillProgressBarRef = null!;


    bool mouseOn = false;

    UnitState? bindingUnitStateRes;

    public override void _Ready() {
        MouseEntered += () => {
            mouseOn = true;
            outlineRef.Visible = true;
        };
        MouseExited += () => {
            mouseOn = false;
            outlineRef.Visible = false;
        };
    }

    public void BindUnitState(UnitState unitState) {
        bindingUnitStateRes = unitState;
    }

    public override void _Process(double delta) {
        containerBuffsRef.UpdateUI_WithUnit(bindingUnitStateRes!);
        hp_StateBarRef.UpdateUI_WithUnit(bindingUnitStateRes!);
        skillProgressBarRef.UpdateUI_WithUnit(bindingUnitStateRes!);
    }


}