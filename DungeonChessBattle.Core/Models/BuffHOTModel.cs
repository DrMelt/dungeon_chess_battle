using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

public class BuffHOTModel : BuffModel {
    public float HealthPerSec { get; set; } = 100.0f;

    protected override void ActionDuration(double deltaTime, IUnitState unitState) {
        if (FromUnit == null)
            throw new InvalidOperationException("[BuffHOTModel] FromUnit has not been initialized.");
        unitState.RestoreHealth(HealthPerSec * (float)deltaTime * FromUnit.CureIntensity);
    }
}
