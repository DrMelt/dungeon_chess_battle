using Godot;
using System;


using GameLogic;
public partial class DecalTargetMark : Decal, I_UI_Update
{
    [Export]
    Color defultColor = new Color("ad9b24");

    [Export]
    UserUISettings userUISettingsRes;


    public override void _Ready()
    {
        SetCampColor(EmunCamp.None);
    }

    public void SetCampColor(EmunCamp camp)
    {
        Color? resColor = userUISettingsRes.GetCampColor(camp);

        if (resColor == null)
        {
            resColor = defultColor;
        }

        Modulate = (Color)resColor;
    }
    public void UpdateUI_WithUnit(UnitGameShow unitShow)
    {
        SetCampColor(unitShow.UnitStateRec.Camp);
    }

    public void SetMark_Normal()
    {
        SetCampColor(EmunCamp.None);
    }

    internal void SetMark_Focus(UnitGameShow unitShow)
    {
        SetCampColor(unitShow.UnitStateRec.Camp);
    }
}
