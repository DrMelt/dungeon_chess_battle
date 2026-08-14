namespace DungeonChessBattle.Battle.Domain.Movement;

/// <summary>
/// 世界坐标中的轴对齐静态障碍矩形，坐标与单位位置同为 XZ 平面。
/// 客户端预测与服务端权威从同一布局构建物理场景，保证移动确定性一致。
/// </summary>
/// <param name="MinX">矩形左边界 X。</param>
/// <param name="MinY">矩形下边界 Y。</param>
/// <param name="MaxX">矩形右边界 X。</param>
/// <param name="MaxY">矩形上边界 Y。</param>
public sealed record ObstacleRect(float MinX, float MinY, float MaxX, float MaxY);

/// <summary>
/// 战场布局：竞技场边界包围盒与静态障碍集合，纯值配置，只读。
/// 副本可配置独立布局，客户端与服务端按副本键取同一定义。
/// </summary>
/// <param name="HalfWidth">竞技场半宽，X 轴向可活动范围为 ±HalfWidth。</param>
/// <param name="HalfHeight">竞技场半高，Y 轴向可活动范围为 ±HalfHeight。</param>
/// <param name="Obstacles">静态障碍矩形集合，可为空。</param>
public sealed record BattlefieldLayout(
    float HalfWidth,
    float HalfHeight,
    IReadOnlyList<ObstacleRect> Obstacles)
{
    /// <summary>副本未配置布局时使用的默认竞技场。</summary>
    public static readonly BattlefieldLayout Default = new(50f, 30f, []);
}
