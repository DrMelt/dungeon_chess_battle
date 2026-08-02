using Godot;

namespace DungeonChessBattle;

public partial class LabelDungeonTime : Label {
    [Export]
    UnitsInScene_Show? unitsInScene_Show_Ref;

    public override void _Ready() {
        if (unitsInScene_Show_Ref == null)
            GD.PrintErr("[LabelDungeonTime] [Export] unitsInScene_Show_Ref is not assigned!");
    }

    public override void _Process(double delta) {
        if (unitsInScene_Show_Ref == null)
            return;
        Text = "Time: " + unitsInScene_Show_Ref.UnitsInSceneRes.SceneTime.ToString("F0");
    }

}
