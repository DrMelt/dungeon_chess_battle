using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

public class SkillAddBuffModel : SkillModel {
    public IBuff Buff { get; set; } = null!;
}
