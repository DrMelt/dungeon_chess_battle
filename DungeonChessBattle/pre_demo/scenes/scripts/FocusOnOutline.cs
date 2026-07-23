using Godot;

namespace DungeonChessBattle;

public partial class FocusOnOutline : Node {
    [Export]
    UserInterfaceRes userInterfaceRes;

    [Export]
    MeshInstance3D outLineMeshInstance;

    public override void _Process(double delta) {
        // Update OutLine
        if (userInterfaceRes.MouseOnUnit != null) {
            outLineMeshInstance.GlobalTransform = userInterfaceRes.MouseOnUnit.UnitMeshInstanceRef.GlobalTransform;
            outLineMeshInstance.Mesh = userInterfaceRes.MouseOnUnit.UnitMeshInstanceRef.Mesh;
        }
        else {
            outLineMeshInstance.Mesh = null;
        }
    }

}
