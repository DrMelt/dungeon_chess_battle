using System.Numerics;

namespace DungeonChessBattle.Battle.Domain.Movement;

/// <summary>
/// 移动所需的只读场景数据（边界/地形/静态障碍）。客户端预测与服务端权威共用同一实现，
/// 从同一关卡配置构建以保证确定性一致。
/// </summary>
public interface IMovementScene {
    /// <summary>从 from 移动到 to 是否可通行（地形/静态障碍判定）。</summary>
    bool IsWalkable(Vector2 from, Vector2 to, float bodyRadius);

    /// <summary>将位置约束到合法范围（边界/禁区）。</summary>
    Vector2 ClampToBounds(Vector2 pos, float bodyRadius);
}
