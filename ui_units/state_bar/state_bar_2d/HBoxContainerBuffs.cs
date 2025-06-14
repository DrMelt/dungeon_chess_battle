using Godot;
using System;

using GameLogic;
public partial class HBoxContainerBuffs : HBoxContainer, I_UI_Update
{
    [Export]
    PackedScene buffIconPackedScene;

    public void UpdateUI_WithUnit(UnitGameShow unitShow)
    {
        var chilren = GetChildren();
        foreach (var child in chilren)
        {
            child.QueueFree();
        }


        if (unitShow == null)
        {
            return;
        }

        UnitState focusUnit = unitShow.UnitStateRec;
        foreach (BuffBase buff in unitShow.UnitStateRec.BuffList)
        {
            TextureRectBuffIcon buffIcon = buffIconPackedScene.Instantiate<TextureRectBuffIcon>();
            buffIcon.SetBuffIcon(buff, focusUnit);
            AddChild(buffIcon);
        }
    }
}
