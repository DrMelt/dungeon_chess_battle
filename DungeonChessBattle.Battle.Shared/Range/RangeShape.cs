using System.Numerics;
using DungeonChessBattle.Battle.Shared.Math;

namespace DungeonChessBattle.Battle.Shared.Range;

/// <summary>
/// 几何范围形状，纯几何判定，不依赖战斗实体。使用 XZ 平面俯视坐标。
/// </summary>
public abstract class RangeShape {
    /// <summary>判断检测点是否处于以锚点为基准、给定朝向的范围内。</summary>
    public abstract bool Contains(Vector2 point, Vector2 anchor, Vector2 direction, float bodyRadius);

    /// <summary>该形状沿朝向的最远有效判定距离，供 AI 逼近决策读取。</summary>
    public abstract float FarReach {
        get;
    }
}

/// <summary>扇形环形范围，以锚点为圆心、沿朝向的角度与半径区间。</summary>
public sealed class SectorShape : RangeShape {
    /// <summary>近端半径。</summary>
    public required float NearClamp {
        get; init;
    }

    /// <summary>远端半径。</summary>
    public required float FarClamp {
        get; init;
    }

    /// <summary>扇形起始角，弧度，以朝向为 0。</summary>
    public float RadianFrom { get; init; } = -MathF.PI;

    /// <summary>扇形结束角，弧度，以朝向为 0。</summary>
    public float RadianTo { get; init; } = MathF.PI;

    /// <inheritdoc />
    public override float FarReach => FarClamp;

    /// <inheritdoc />
    public override bool Contains(Vector2 point, Vector2 anchor, Vector2 direction, float bodyRadius) {
        direction = Vector2.Normalize(direction);
        Vector2 toCheck = point - anchor;
        float d = toCheck.Length();

        // 距离条件：圆与环形 [nearClamp, farClamp] 有交集
        if (d + bodyRadius < NearClamp || d - bodyRadius > FarClamp)
            return false;

        // 角度条件：圆包含锚点时角度条件恒成立
        if (d <= bodyRadius)
            return true;

        Vector2 dirToCheck = toCheck / d;
        float tanX = Vector2.Dot(dirToCheck, direction);
        float tanY = VectorMath.Cross(dirToCheck, direction);
        float centerAngle = MathF.Atan2(tanY, tanX);
        float halfAngle = MathF.Asin(MathF.Min(bodyRadius / d, 1f));

        return AngularIntervalsOverlap(centerAngle - halfAngle, centerAngle + halfAngle, RadianFrom, RadianTo);
    }

    /// <summary>判断两个角度区间是否有交集，弧度，边界可能超出 [-π, π]。</summary>
    private static bool AngularIntervalsOverlap(float aFrom, float aTo, float bFrom, float bTo) {
        const float twoPi = 2f * MathF.PI;
        aFrom = NormalizePositive(aFrom);
        aTo = NormalizePositive(aTo);
        bFrom = NormalizePositive(bFrom);
        bTo = NormalizePositive(bTo);

        float aLen = aTo - aFrom;
        if (aLen < 0f)
            aLen += twoPi;
        float bLen = bTo - bFrom;
        if (bLen < 0f)
            bLen += twoPi;

        float distCW = bFrom - aFrom;
        if (distCW < 0f)
            distCW += twoPi;

        return distCW <= aLen || distCW + bLen <= aLen + twoPi;
    }

    private static float NormalizePositive(float angle) {
        const float twoPi = 2f * MathF.PI;
        angle %= twoPi;
        return angle < 0f ? angle + twoPi : angle;
    }
}

/// <summary>矩形范围：以锚点为近端起点、沿朝向向前延伸的矩形。</summary>
public sealed class RectShape : RangeShape {
    /// <summary>近端沿朝向的边界。</summary>
    public required float NearClamp {
        get; init;
    }

    /// <summary>远端沿朝向的边界。</summary>
    public required float FarClamp {
        get; init;
    }

    /// <summary>左侧横向边界。</summary>
    public float FromLeft { get; init; } = -1.0f;

    /// <summary>右侧横向边界。</summary>
    public float ToRight { get; init; } = 1.0f;

    /// <inheritdoc />
    public override float FarReach => FarClamp;

    /// <inheritdoc />
    public override bool Contains(Vector2 point, Vector2 anchor, Vector2 direction, float bodyRadius) {
        direction = Vector2.Normalize(direction);
        Vector2 toCheck = point - anchor;

        float tanX = Vector2.Dot(toCheck, direction);
        float tanY = VectorMath.Cross(toCheck, direction);

        float closestX = System.Math.Clamp(tanX, NearClamp, FarClamp);
        float closestY = System.Math.Clamp(tanY, FromLeft, ToRight);
        float dx = tanX - closestX;
        float dy = tanY - closestY;

        return dx * dx + dy * dy <= bodyRadius * bodyRadius;
    }
}
