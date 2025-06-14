using Godot;
using Godot.Collections;
using System;

using GameLogic;


[GlobalClass]
public partial class UserUISettings : Resource
{
    [Export]
    Dictionary<EmunCamp, Color> campColorDict = new Dictionary<EmunCamp, Color>();
    public Color? GetCampColor(EmunCamp camp)
    {
        var tryRes = campColorDict.TryGetValue(camp, out Color campColor);
        return tryRes ? campColor : null;
    }
}
