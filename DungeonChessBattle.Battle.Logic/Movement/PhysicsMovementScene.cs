using System.Numerics;
using DungeonChessBattle.Battle.Shared.Movement;
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
/// 在线与回放从同一布局构建，结算结果一致。
/// 移动本体由 <see cref="MovementMath"/> 纯函数驱动，Aether 仅承担静态几何
/// 宽相查询，不运行动态模拟，天然适配 LES 回滚重放。
/// 解算只消费本轮 <see cref="MoveIntent"/> 快照，不持有单位引用，顺序无关。
/// </summary>
public sealed class PhysicsMovementScene : IMovementScene {
    /// <summary>单次推挤结算的最大位移步长，防止快速单位穿透细薄障碍。</summary>
    private const float SubStepLength = 0.5f;

    private readonly AetherWorld _world;

    /// <summary>竞技场半宽，X 轴向 ±HalfWidth 为可活动范围。</summary>
    private readonly float _halfWidth;

    /// <summary>竞技场半高，Y 轴向 ±HalfHeight 为可活动范围。</summary>
    private readonly float _halfHeight;

    /// <summary>Aether fixture 到布局障碍的映射，宽相查询命中后取几何数据。</summary>
    private readonly Dictionary<AetherFixture, ObstacleRect> _obstacleByFixture = [];

    /// <summary>宽相查询结果暂存，单次推挤复用以避免重复分配。</summary>
    private readonly List<ObstacleRect> _queryResults = [];

    /// <summary>宽相查询回调，避免逐次构造委托。</summary>
    private readonly AetherQueryReport _queryCallback;

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
    public IReadOnlyList<Vector2> Resolve(IReadOnlyList<MoveIntent> intents, float dt) {
        var count = intents.Count;
        var positions = new Vector2[count];
        for (var i = 0; i < count; i++)
            positions[i] = intents[i].FromPosition;

        // 单位互斥：在本轮起始位置快照上就地逐对让位，位移途中不再互相检测
        MovementMath.ResolveExclusion(positions, intents);

        var results = new Vector2[count];
        for (var i = 0; i < count; i++)
            results[i] = Advance(intents[i], positions[i], dt);
        return results;
    }

    /// <summary>单单位推进：位移增量细分步进，每步先静态障碍推挤，末段边界约束。</summary>
    private Vector2 Advance(MoveIntent intent, Vector2 start, float dt) {
        var delta = MovementMath.Displacement(intent.Direction, intent.Speed, dt);
        var total = delta.Length();
        if (total <= 1e-6f)
            return MovementMath.ClampToBounds(start, intent.BodyRadius, _halfWidth, _halfHeight);

        // 按固定步长细分位移，防快速单位隧穿细薄障碍
        var segments = Math.Max(1, (int)MathF.Ceiling(total / SubStepLength));
        var step = delta * (1f / segments);
        var pos = start;
        for (var i = 0; i < segments; i++)
            pos = ResolveObstacles(pos + step, intent.BodyRadius);
        return MovementMath.ClampToBounds(pos, intent.BodyRadius, _halfWidth, _halfHeight);
    }

    /// <summary>单步静态障碍推挤：Aether 宽相查询命中候选，再做精确圆↔矩形推挤。</summary>
    private Vector2 ResolveObstacles(Vector2 pos, float bodyRadius) {
        var aabb = new AetherAABB(
            new AetherVector2(pos.X - bodyRadius, pos.Y - bodyRadius),
            new AetherVector2(pos.X + bodyRadius, pos.Y + bodyRadius));
        _queryResults.Clear();
        _world.QueryAABB(_queryCallback, ref aabb);
        foreach (var rect in _queryResults)
            pos = MovementMath.PushOutCircleRect(pos, bodyRadius, rect);
        return pos;
    }

    /// <summary>宽相查询回调：收集命中布局障碍的矩形几何。</summary>
    private bool QueryFixtures(AetherFixture fixture) {
        if (_obstacleByFixture.TryGetValue(fixture, out var rect))
            _queryResults.Add(rect);
        return true;
    }
}
