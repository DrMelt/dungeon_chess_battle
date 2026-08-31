using System.Numerics;
using DungeonChessBattle.Battle.Shared.Movement;

namespace DungeonChessBattle.Battle.Logic.Movement;

/// <summary>
/// 移动解算纯函数层：方向位移增量、两两对称同时互斥推挤、圆↔矩形推挤与边界约束。
/// 无状态、不依赖物理引擎与领域实体，输入值返回新值，在线与回放共用同一确定性实现。
/// </summary>
public static class MovementMath {
    /// <summary>位移增量：方向 * 速度 * 时间。</summary>
    public static Vector2 Displacement(Vector2 direction, float speed, float dt)
        => direction * speed * dt;

    /// <summary>
    /// 两两互斥：固定轮数内逐对处理，重叠双方各沿连线让位一半；完全重叠时沿 +X 分离。
    /// 就地更新工作位置，后处理的对读到前面让位后的值，故结果依赖数组顺序。
    /// </summary>
    public static void ResolveExclusion(Vector2[] positions, IReadOnlyList<MoveIntent> intents, int iterations = 3) {
        for (int it = 0; it < iterations; it++) {
            for (int i = 0; i < positions.Length; i++) {
                for (int j = i + 1; j < positions.Length; j++) {
                    float min = intents[i].BodyRadius + intents[j].BodyRadius;
                    var delta = positions[j] - positions[i];
                    float distSq = delta.LengthSquared();
                    if (distSq >= min * min)
                        continue;

                    float dist = MathF.Sqrt(distSq);
                    if (dist <= 1e-6f) {
                        var push = new Vector2(min * 0.5f, 0f);
                        positions[i] -= push;
                        positions[j] += push;
                    }
                    else {
                        var normal = delta / dist;
                        var push = normal * ((min - dist) * 0.5f);
                        positions[i] -= push;
                        positions[j] += push;
                    }
                }
            }
        }
    }

    /// <summary>圆与轴对齐矩形推挤：清除嵌入，推出至恰好外接。</summary>
    public static Vector2 PushOutCircleRect(Vector2 circle, float radius, ObstacleRect rect) {
        var closestX = Math.Clamp(circle.X, rect.MinX, rect.MaxX);
        var closestY = Math.Clamp(circle.Y, rect.MinY, rect.MaxY);
        var diff = new Vector2(circle.X - closestX, circle.Y - closestY);
        var distSq = diff.LengthSquared();

        if (distSq >= radius * radius)
            return circle;

        // 圆心落入矩形内部：沿穿透最小的轴推出
        if (distSq <= 1e-6f) {
            var pushLeft = circle.X - rect.MinX;
            var pushRight = rect.MaxX - circle.X;
            var pushDown = circle.Y - rect.MinY;
            var pushUp = rect.MaxY - circle.Y;
            if (pushLeft <= pushRight && pushLeft <= pushDown && pushLeft <= pushUp)
                return new Vector2(rect.MinX - radius, circle.Y);
            if (pushRight <= pushDown && pushRight <= pushUp)
                return new Vector2(rect.MaxX + radius, circle.Y);
            if (pushDown <= pushUp)
                return new Vector2(circle.X, rect.MinY - radius);
            return new Vector2(circle.X, rect.MaxY + radius);
        }

        return new Vector2(closestX, closestY) + diff * (radius / MathF.Sqrt(distSq));
    }

    /// <summary>约束位置到竞技场边界内，保持与边界的单位半径间距。</summary>
    public static Vector2 ClampToBounds(Vector2 pos, float bodyRadius, float halfWidth, float halfHeight) {
        var minX = -halfWidth + bodyRadius;
        var maxX = halfWidth - bodyRadius;
        var minY = -halfHeight + bodyRadius;
        var maxY = halfHeight - bodyRadius;
        return new Vector2(
            minX > maxX ? 0f : Math.Clamp(pos.X, minX, maxX),
            minY > maxY ? 0f : Math.Clamp(pos.Y, minY, maxY));
    }
}
