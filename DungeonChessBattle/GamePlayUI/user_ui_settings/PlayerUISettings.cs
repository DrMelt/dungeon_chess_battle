using DungeonChessBattle.Protocol.Enums;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 玩家 UI 颜色设置资源，配置状态信息与阵营相关颜色。
/// </summary>
[GlobalClass]
public partial class PlayerUISettings : Resource {
    /// <summary>生命值信息颜色。</summary>
    [ExportGroup("State Info")]
    [Export]
    public Color HealthInfoColor { get; private set; } = new(1, 1, 1, 1);

    /// <summary>物理伤害信息颜色。</summary>
    [Export]
    public Color PhysicalInfoColor { get; private set; } = new(1, 1, 1, 1);

    /// <summary>魔法伤害信息颜色。</summary>
    [Export]
    public Color MagicInfoColor { get; private set; } = new(1, 1, 1, 1);

    /// <summary>友方阵营颜色。</summary>
    [ExportGroup("Camp Info")]
    [Export]
    public Color AllyCampColor { get; private set; } = new(1, 1, 1, 1);

    /// <summary>中立阵营颜色。</summary>
    [Export]
    public Color NeutralCampColor { get; private set; } = new(1, 1, 1, 1);

    /// <summary>敌方阵营颜色。</summary>
    [Export]
    public Color EnemyCampColor { get; private set; } = new(1, 1, 1, 1);

    /// <summary>
    /// 根据阵营标识获取对应颜色；空或未知阵营返回中立色。
    /// </summary>
    /// <param name="camp">阵营标识。</param>
    /// <returns>对应的阵营颜色。</returns>
    public Color? GetCampColor(string camp) {
        if (string.IsNullOrEmpty(camp))
            return NeutralCampColor;
        return camp switch {
            CampConstants.CampA => AllyCampColor,
            CampConstants.CampB => EnemyCampColor,
            CampConstants.CampBoss => NeutralCampColor,
            _ => AllyCampColor
        };
    }
}
