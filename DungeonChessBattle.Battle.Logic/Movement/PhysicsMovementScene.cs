using System.Numerics;
using DungeonChessBattle.Battle.Domain.Movement;
using AetherWorld = nkast.Aether.Physics2D.Dynamics.World;
using AetherBodyType = nkast.Aether.Physics2D.Dynamics.BodyType;
using AetherFixture = nkast.Aether.Physics2D.Dynamics.Fixture;
using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;
using AetherAABB = nkast.Aether.Physics2D.Collision.AABB;
using AetherQueryReport = nkast.Aether.Physics2D.Dynamics.QueryReportFixtureDelegate;

namespace DungeonChessBattle.Battle.Logic.Movement;

/// <summary>
/// 基于 Aether.Physics2D 的确定性移动场景：以战场布局构建静态障碍物理世界，
/// 提供竞技场边界约束、静态障碍推挤与单位互斥。
/// 客户端预测与服务端权威从同一布局构建，结算结果一致。
/// 移动本体由 <see cref="MovementResolver"/> 纯函数驱动，Aether 仅承担静态几何
/// 宽相查询，不运行动态模拟，天然适配 LES 回滚重放。
/// </summary>
public sealed class PhysicsMovementScene : IMovementScene, IActorRegistration {
    /// <summary>单次推挤结算的最大位移步长，防止快速单位穿透细薄障碍。</summary>
    private const float SubStepLength = 0.5f;

    private readonly AetherWorld _world;

    /// <summary>竞技场半宽，X 轴向 ±HalfWidth 为可活动范围。</summary>
    private readonly float _halfWidth;

    /// <summary>竞技场半高，Y 轴向 ±HalfHeight 为可活动范围。</summary>
    private readonly float _halfHeight;

    /// <summary>Aether fixture 到布局障碍的映射，宽相查询命中后取几何数据。</summary>
    private readonly Dictionary<AetherFixture, ObstacleRect> _obstacleByFixture = [];

    /// <summary>参与单位互斥的演员，按唯一键升序遍历保证跨端结算顺序一致。</summary>
    private readonly SortedDictionary<ushort, ActorRecord> _actors = [];

    /// <summary>宽相查询结果暂存，单次推挤复用以避免重复分配。</summary>
    private readonly List<ObstacleRect> _queryResults = [];

    /// <summary>宽相查询回调，避免逐次构造委托。</summary>
    private readonly AetherQueryReport _queryCallback;

    private sealed record ActorRecord(Func<float> Radius, Func<Vector2> Position);

    /// <summary>从布局构建物理场景：静态障碍写入 Aether World，边界尺寸本地保存。</summary>
    public PhysicsMovementScene(BattlefieldLayout layout) {
        ArgumentNullException.ThrowIfNull(layout);
        _halfWidth = layout.HalfWidth;
        _halfHeight = layout.HalfHeight;
        _world = new AetherWorld(AetherVector2.Zero);
        _queryCallback = QueryFixtures;

        foreach (var rect in layout.Obstacles) {
            var width = rect.MaxX - rect.MinX;
            var height = rect.MaxY - rect.MinY;
            if (width <= 0f || height <= 0f)
                continue;

            // 静态障碍只参与宽相查询，不进入动态模拟
            var center = new AetherVector2((rect.MinX + rect.MaxX) * 0.5f, (rect.MinY + rect.MaxY) * 0.5f);
            var body = _world.CreateBody(center, 0f, AetherBodyType.Static);
            var fixture = body.CreateRectangle(width, height, 1f, default);
            _obstacleByFixture[fixture] = rect;
        }
    }

    /// <inheritdoc />
    public void AddActor(ushort actorId, Func<float> radiusProvider, Func<Vector2> positionProvider) {
        _actors[actorId] = new ActorRecord(radiusProvider, positionProvider);
    }

    /// <inheritdoc />
    public void RemoveActor(ushort actorId) {
        _actors.Remove(actorId);
    }

    /// <inheritdoc />
    public Vector2 ResolveMove(ushort actorId, Vector2 from, Vector2 delta, float bodyRadius) {
        var total = delta.Length();
        if (total <= 1e-6f)
            return ClampToBounds(from, bodyRadius);

        // 按固定步长细分位移，防快速单位隧穿细薄障碍
        var segments = Math.Max(1, (int)MathF.Ceiling(total / SubStepLength));
        var step = delta * (1f / segments);
        var pos = from;
        for (var i = 0; i < segments; i++)
            pos = ResolveCollisions(actorId, pos + step, bodyRadius);
        return ClampToBounds(pos, bodyRadius);
    }

    /// <summary>单步推挤结算：先单位互斥，后静态障碍推挤。</summary>
    private Vector2 ResolveCollisions(ushort actorId, Vector2 pos, float bodyRadius) {
        // 单位互斥：半径与位置实时读取权威状态，回滚重放时自然还原
        foreach (var pair in _actors) {
            if (pair.Key == actorId)
                continue;
            var actor = pair.Value;
            var otherPos = actor.Position();
            var minDistance = bodyRadius + actor.Radius();
            var diff = pos - otherPos;
            var distSq = diff.LengthSquared();
            if (distSq >= minDistance * minDistance)
                continue;
            // 完全重叠时沿 +X 推开，避免除零
            pos = distSq <= 1e-6f
                ? new Vector2(otherPos.X + minDistance, otherPos.Y)
                : otherPos + diff * (minDistance / MathF.Sqrt(distSq));
        }

        // 静态障碍：Aether 宽相查询命中候选，再做精确圆↔矩形推挤
        var aabb = new AetherAABB(
            new AetherVector2(pos.X - bodyRadius, pos.Y - bodyRadius),
            new AetherVector2(pos.X + bodyRadius, pos.Y + bodyRadius));
        _queryResults.Clear();
        _world.QueryAABB(_queryCallback, ref aabb);
        foreach (var rect in _queryResults)
            pos = PushOutCircleRect(pos, bodyRadius, rect);
        return pos;
    }

    /// <summary>宽相查询回调：收集命中布局障碍的矩形几何。</summary>
    private bool QueryFixtures(AetherFixture fixture) {
        if (_obstacleByFixture.TryGetValue(fixture, out var rect))
            _queryResults.Add(rect);
        return true;
    }

    /// <summary>圆与轴对齐矩形推挤：清除嵌入，推出至恰好外接。</summary>
    private static Vector2 PushOutCircleRect(Vector2 circle, float radius, ObstacleRect rect) {
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
    private Vector2 ClampToBounds(Vector2 pos, float bodyRadius) {
        var minX = -_halfWidth + bodyRadius;
        var maxX = _halfWidth - bodyRadius;
        var minY = -_halfHeight + bodyRadius;
        var maxY = _halfHeight - bodyRadius;
        return new Vector2(
            minX > maxX ? 0f : Math.Clamp(pos.X, minX, maxX),
            minY > maxY ? 0f : Math.Clamp(pos.Y, minY, maxY));
    }
}
