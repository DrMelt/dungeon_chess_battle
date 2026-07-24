using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Buff_HOT : BuffBaseGodot {
    [Export]
    float healthPerSec = 100.0f;

    protected override BuffModel CreateModel() {
        return new BuffHOTModel {
            HealthPerSec = healthPerSec,
        };
    }
}
