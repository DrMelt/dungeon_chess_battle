using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Skill_Add_BUFF : UnitSkillBaseGodot {
    [Export]
    BuffBaseGodot buff = null!;

    protected override SkillModel CreateModel() {
        return new SkillAddBuffModel {
            Buff = buff,
        };
    }
}
