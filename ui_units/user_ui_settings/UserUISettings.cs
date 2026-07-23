using Godot;
using Godot.Collections;

using DungeonChessBattle.Core;

namespace DungeonChessBattle;

[GlobalClass]
public partial class UserUISettings : Resource {
    [ExportGroup("State Info")]
    [Export]
    Color healthInfoColor = new(1, 1, 1, 1);
    public Color HealthInfoColor => healthInfoColor;
    [Export]
    Color physicalInfoColor = new(1, 1, 1, 1);
    public Color PhysicalInfoColor => physicalInfoColor;
    [Export]
    Color magicInfoColor = new(1, 1, 1, 1);
    public Color MagicInfoColor => magicInfoColor;

    [ExportGroup("Camp Info")]
    [Export]
    Dictionary<EnumCamp, Color> campColorDict = [];
    public Color? GetCampColor(EnumCamp camp) {
        bool tryRes = campColorDict.TryGetValue(camp, out Color campColor);
        return tryRes ? campColor : null;
    }
}
