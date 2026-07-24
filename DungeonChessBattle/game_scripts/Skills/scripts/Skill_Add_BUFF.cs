using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Skill_Add_BUFF : UnitSkillBaseGodot {
    [Export]
    BuffBaseGodot buff;

    protected override SkillModel CreateModel() {
        return new SkillAddBuffModel {
            Buff = buff,
        };
    }
}
