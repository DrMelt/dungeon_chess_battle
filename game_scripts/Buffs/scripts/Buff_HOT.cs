using Godot;
using System;


namespace GameLogic
{
    [GlobalClass]
    public partial class Buff_HOT : BuffBase
    {
        [Export]
        float healthPerSec = 100.0f;

        protected override void ActionDuration(double deltaTime, UnitState unitState)
        {
            unitState.RestoreHealth(healthPerSec * (float)deltaTime * fromUnit.CureIntensity);
        }
    }
}