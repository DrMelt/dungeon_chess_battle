using Godot;
using System;


namespace GameLogic {
    [GlobalClass]
    public partial class Buff_DOT : BuffBase {
        [Export]
        Enum_DamageType damageType;

        [Export]
        float damagePerSec = 100.0f;

        protected override void ActionDuration(double deltaTime, UnitState unitState) {
            unitState.TakeDamage((float)deltaTime * fromUnit.MagicDamageAmount(damagePerSec), damageType);
        }
    }
}
