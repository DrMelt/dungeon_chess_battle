using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 施加 Buff 的技能模型：释放时为目标单位添加一个 Buff。
/// </summary>
public class SkillAddBuffModel : SkillModel {
    /// <summary>释放时施加给目标的 Buff。</summary>
    public IBuff? Buff {
        get; set;
    }
}
