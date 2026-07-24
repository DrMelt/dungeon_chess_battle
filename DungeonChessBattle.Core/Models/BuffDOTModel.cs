using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

public class BuffDOTModel : BuffModel {
    public Enum_DamageType DamageType {
        get; set;
    }
    public float DamagePerSec { get; set; } = 100.0f;

    protected override void ActionDuration(double deltaTime, IUnitState unitState) {
        unitState.TakeDamage((float)deltaTime * FromUnit.MagicDamageAmount(DamagePerSec), DamageType);
    }
}
