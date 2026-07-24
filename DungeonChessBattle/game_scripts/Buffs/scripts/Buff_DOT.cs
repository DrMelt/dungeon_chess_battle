using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Buff_DOT : BuffBaseGodot {
    [Export]
    Enum_DamageType damageType;

    [Export]
    float damagePerSec = 100.0f;

    protected override BuffModel CreateModel() {
        return new BuffDOTModel {
            DamageType = damageType,
            DamagePerSec = damagePerSec,
        };
    }
}
