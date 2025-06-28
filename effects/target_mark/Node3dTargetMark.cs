using GameLogic;
using Godot;
using System;

public partial class Node3dTargetMark : Node3D, I_UI_Update {
    [Export]
    UserInterfaceRes userInterfaceRes;

    [Export]
    Decal targetDecalRef;
    public Decal TargetDecalRef => targetDecalRef;

    [Export]
    Color defultColor = new Color("ad9b24");

    [Export]
    UserUISettings userUISettingsRes;


    public override void _Ready() {
        SetCampColor(EnumCamp.None);
    }

    public void SetCampColor(EnumCamp camp) {
        Color? resColor = userUISettingsRes.GetCampColor(camp);

        if (resColor == null) {
            resColor = defultColor;
        }

        targetDecalRef.Modulate = (Color)resColor;
    }
    public void UpdateUI_WithUnit(UnitState unitState) {
        if (userInterfaceRes.FocusOnUnit != null && unitState == userInterfaceRes.FocusOnUnit.UnitStateRec) {
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
