using Godot;

namespace DungeonChessBattle;

public partial class LabelDungeonTime : Label {
    [Export]
    UnitsInScene_Show unitsInScene_Show_Ref = null!;

    public override void _Process(double delta) {
        Text = "Time: " + unitsInScene_Show_Ref.UnitsInSceneRes.SceneTime.ToString("F0");
    }

}
