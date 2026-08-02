using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.ui_units.ui_interface;
using Godot;

namespace DungeonChessBattle;

public partial class Node3dTargetMark : Node3D, IUI_Update {
    public Node3dTargetMarkInterRefs? InterRefs {
        get; private set;
    }
    public Decal TargetDecalRef => InterRefs!.TargetDecalRef;

    public override void _Ready() {
        InterRefs = GetNode<Node3dTargetMarkInterRefs>("Node3dTargetMarkInterRefs");
        SetCampColor(EnumCamp.None);
    }

    public void SetCampColor(EnumCamp camp) {
        Color? resColor = InterRefs!.UserUISettingsRes.GetCampColor(camp);

        resColor ??= InterRefs!.DefultColor;

        InterRefs!.TargetDecalRef.Modulate = (Color)resColor;
    }
    public void UpdateUI_WithUnit(UnitState unitState) {
        if (InterRefs!.UserInterfaceRes.FocusOnUnit != null && unitState == InterRefs!.UserInterfaceRes.FocusOnUnit.UnitStateRec) {
            SetCampColor(unitState.Camp);
        }
        else {
            SetCampColor(EnumCamp.None);
        }

        Scale = new Vector3(unitState.BodyRadius, 1, unitState.BodyRadius);
    }

    public void SetMark_Normal() {
        SetCampColor(EnumCamp.None);
    }

    internal void SetMark_Focus(UnitGameShow unitShow) {
        SetCampColor(unitShow.UnitStateRec.Camp);
    }

}
