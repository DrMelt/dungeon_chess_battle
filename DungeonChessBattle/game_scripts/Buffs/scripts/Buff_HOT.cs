using DungeonChessBattle.Core.Interfaces;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Buff_HOT : BuffBaseGodot {
    [Export]
    float healthPerSec = 100.0f;

    protected override void ActionDuration(double deltaTime, IUnitState unitState) {
        unitState.RestoreHealth(healthPerSec * (float)deltaTime * fromUnit.CureIntensity);
    }
}
