using Godot;

using DungeonChessBattle.Core;
using DungeonChessBattle.ui_units.ui_interface;

namespace DungeonChessBattle;

public partial class ContainerBuffs : Control, IUI_Update {
    [Export]
    PackedScene buffIconPackedScene;

    public void UpdateUI_WithUnit(UnitState unitState) {
        var chilren = GetChildren();
        foreach (var child in chilren) {
            child.QueueFree();
        }


        if (unitState == null) {
            return;
        }

        foreach (BuffBaseGodot buff in unitState.BuffList) {
            TextureRectBuffIcon buffIcon = buffIconPackedScene.Instantiate<TextureRectBuffIcon>();
            buffIcon.SetBuffIcon(buff, unitState);
            AddChild(buffIcon);
        }
    }
}
