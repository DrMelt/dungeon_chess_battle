using Godot;
using DungeonChessBattle.ui_units.ui_interface;

namespace DungeonChessBattle;

public partial class ContainerBuffs : Control, IUI_Update {
    public ContainerBuffsInterRefs? InterRefs {
        get; private set;
    }

    public override void _Ready() {
        InterRefs = GetNode<ContainerBuffsInterRefs>("ContainerBuffsInterRefs");
    }

    public void UpdateUI_WithUnit(UnitState unitState) {
        var chilren = GetChildren();
        foreach (var child in chilren) {
            child.QueueFree();
        }


        if (unitState == null) {
            return;
        }
        if (InterRefs?.BuffIconPackedScene == null) {
            return;
        }

        foreach (BuffBaseGodot buff in unitState.BuffList) {
            TextureRectBuffIcon buffIcon = InterRefs.BuffIconPackedScene.Instantiate<TextureRectBuffIcon>();
            buffIcon.SetBuffIcon(buff, unitState);
            AddChild(buffIcon);
        }
    }
}
