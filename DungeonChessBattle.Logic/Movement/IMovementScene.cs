using System.Numerics;

namespace DungeonChessBattle.Logic.Movement;

/// <summary>
/// 移动所需的只读场景数据（边界/地形/静态障碍）。
/// 服务端与客户端从同一关卡配置构建，保证两端一致，从而预测与权威确定性一致。
/// 客户端预测仅使用静态场景；单位间动态碰撞由服务端权威兜底（回滚纠偏）。
/// </summary>
public interface IMovementScene {
    /// <summary>
    /// 静态阻挡判定：从 from 移动到 to 是否可通行（地形/静态障碍）。
    /// </summary>
    /// <param name="from">移动前位置。</param>
    /// <param name="to">期望到达位置。</param>
    /// <param name="bodyRadius">单位碰撞半径。</param>
    /// <returns>可通行返回 true；受阻返回 false。</returns>
    bool IsWalkable(Vector2 from, Vector2 to, float bodyRadius);

    /// <summary>
    /// 将位置约束到合法范围（边界/禁区）。
    /// </summary>
    /// <param name="pos">待约束位置。</param>
    /// <param name="bodyRadius">单位碰撞半径。</param>
    /// <returns>约束后的合法位置。</returns>
    Vector2 ClampToBounds(Vector2 pos, float bodyRadius);
}
