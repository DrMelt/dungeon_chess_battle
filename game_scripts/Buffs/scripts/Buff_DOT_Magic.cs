using Godot;
using System;


namespace GameLogic
{
    [GlobalClass]
    public partial class Buff_DOT_Magic : BuffBase
    {
        [Export]
        float damagePerSec = 100.0f;

        protected override void ActionDuration(double deltaTime, UnitState unitState)
        {
            unitState.TakeDamage(0, damagePerSec * (float)deltaTime * fromUnit.PhysicalAttackBase);
        }
    }
}