using DungeonChessBattle.Core.Interfaces;
using Godot;


namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Buff_DOT : BuffBaseGodot {
    [Export]
    Enum_DamageType damageType;

    [Export]
    float damagePerSec = 100.0f;

    protected override void ActionDuration(double deltaTime, IUnitState unitState) {
        unitState.TakeDamage((float)deltaTime * fromUnit.MagicDamageAmount(damagePerSec), damageType);
    }
}
