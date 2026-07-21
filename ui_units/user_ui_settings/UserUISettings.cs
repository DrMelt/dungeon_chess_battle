using Godot;
using Godot.Collections;
using System;

using GameLogic;


[GlobalClass]
public partial class UserUISettings : Resource {
    [ExportGroup("State Info")]
    [Export]
    Color healthInfoColor = new Color(1, 1, 1, 1);
    public Color HealthInfoColor => healthInfoColor;
    [Export]
    Color physicalInfoColor = new Color(1, 1, 1, 1);
    public Color PhysicalInfoColor => physicalInfoColor;
    [Export]
    Color magicInfoColor = new Color(1, 1, 1, 1);
    public Color MagicInfoColor => magicInfoColor;

    [ExportGroup("Camp Info")]
    [Export]
    Godot.Collections.Dictionary<EnumCamp, Color> campColorDict = new Godot.Collections.Dictionary<EnumCamp, Color>();
    public Color? GetCampColor(EnumCamp camp) {
        bool tryRes = campColorDict.TryGetValue(camp, out Color campColor);
        return tryRes ? campColor : null;
    }
}
