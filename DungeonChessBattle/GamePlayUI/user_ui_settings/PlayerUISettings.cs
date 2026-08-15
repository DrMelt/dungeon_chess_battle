using DungeonChessBattle.Battle.Domain.Enums;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

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

    /// <summary>阵营判定未就绪/未知时的灰色，表示关系尚未观测到。</summary>
    [Export]
    public Color UndeterminedColor { get; private set; } = new(0.5f, 0.5f, 0.5f, 1f);

    /// <summary>
    /// 按目标相对本地玩家的阵营关系取色，关系色唯一的映射点。
    /// 未判定（Unknown）映射到灰色，不借用中立色。
    /// </summary>
    /// <param name="relation">目标相对本地玩家的阵营关系。</param>
    /// <returns>对应的阵营颜色。</returns>
    public Color GetRelationColor(CampRelation relation) => relation switch {
        CampRelation.Friendly => AllyCampColor,
        CampRelation.Enemy => EnemyCampColor,
        CampRelation.Unknown => UndeterminedColor,
        _ => NeutralCampColor,
    };
}
