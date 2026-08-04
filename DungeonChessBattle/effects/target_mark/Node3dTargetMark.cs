using System;
using System.Linq;
using DungeonChessBattle.InGameUI.ui_interface;
using Godot;

namespace DungeonChessBattle;

public partial class Node3dTargetMark : Node3D, IUI_Update {
    public Node3dTargetMarkInterRefs? InterRefs {
        get; private set;
    }

    private Node3dTargetMarkInterRefs InterRefsOrThrow =>
        InterRefs ?? throw new InvalidOperationException("[Node3dTargetMark] InterRefs has not been initialized.");

    public Decal? TargetDecalRef => InterRefsOrThrow.TargetDecalRef;

    public override void _Ready() {
        InterRefs = GetNode<Node3dTargetMarkInterRefs>("Node3dTargetMarkInterRefs");
        SetCampColor("");
    }

    public void SetCampColor(string camp) {
        var interRefs = InterRefsOrThrow;
        var uiSettings = interRefs.UserUISettingsRes
            ?? throw new InvalidOperationException("[Node3dTargetMark] UserUISettingsRes is not assigned.");
        var targetDecal = interRefs.TargetDecalRef
            ?? throw new InvalidOperationException("[Node3dTargetMark] TargetDecalRef is not assigned.");
        Color? resColor = uiSettings.GetCampColor(camp);

        resColor ??= interRefs.DefultColor;

        targetDecal.Modulate = (Color)resColor;
    }
    public void UpdateUI_WithUnit(UnitState unitState) {
        var interRefs = InterRefsOrThrow;
        var uiRes = interRefs.UserInterfaceRes
            ?? throw new InvalidOperationException("[Node3dTargetMark] UserInterfaceRes is not assigned.");
        if (uiRes.FocusOnUnit != null && unitState == uiRes.FocusOnUnit.UnitStateRec) {
            SetCampColor(unitState.Camps.FirstOrDefault() ?? "");
        }
        else {
            SetCampColor("");
        }

        Scale = new Vector3(unitState.BodyRadius, 1, unitState.BodyRadius);
    }

    public void SetMark_Normal() {
        SetCampColor("");
    }

    internal void SetMark_Focus(UnitGameShow unitShow) {
        SetCampColor(unitShow.UnitStateRec.Camps.FirstOrDefault() ?? "");
    }
}
