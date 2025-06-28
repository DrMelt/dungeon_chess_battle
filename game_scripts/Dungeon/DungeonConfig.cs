using Godot;
using System;


[GlobalClass]
public partial class DungeonConfig : Resource {
    [Export]
    PackedScene dungeonEnvPKS;
    public DungeonEnv DungeonEnvRef {
        get {
            return dungeonEnvPKS.Instantiate<DungeonEnv>();
        }
    }

}
