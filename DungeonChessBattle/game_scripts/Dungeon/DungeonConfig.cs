using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class DungeonConfig : Resource {
    [Export]
    PackedScene dungeonEnvPKS = null!;
    public DungeonEnv DungeonEnvRef {
        get {
            return dungeonEnvPKS.Instantiate<DungeonEnv>();
        }
    }

}
