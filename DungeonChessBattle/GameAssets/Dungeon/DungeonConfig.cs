using Godot;
using System;

namespace DungeonChessBattle;

[GlobalClass]
public partial class DungeonConfig : Resource {
    [Export]
    PackedScene? dungeonEnvPKS;

    public DungeonEnv DungeonEnvRef {
        get {
            if (dungeonEnvPKS == null)
                throw new InvalidOperationException("[DungeonConfig] [Export] dungeonEnvPKS is not assigned!");
            return dungeonEnvPKS.Instantiate<DungeonEnv>();
        }
    }
}
