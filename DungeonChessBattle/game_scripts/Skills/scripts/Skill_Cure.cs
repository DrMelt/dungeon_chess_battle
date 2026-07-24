using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Skill_Cure : UnitSkillBaseGodot {
    [Export]
    float curePotency = 0;

    protected override SkillModel CreateModel() {
        return new SkillCureModel {
            CurePotency = curePotency,
        };
    }
}
