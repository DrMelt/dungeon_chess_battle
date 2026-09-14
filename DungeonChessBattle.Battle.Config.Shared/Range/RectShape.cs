using System.Numerics;
using DungeonChessBattle.Battle.Shared.Math;
using DungeonChessBattle.Battle.Shared.Range;

namespace DungeonChessBattle.Battle.Config.Shared.Range;

/// <summary>矩形范围：以锚点为近端起点、沿朝向向前延伸的矩形。</summary>
public sealed class RectShape : IRangeShape {
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
    public float FarReach => FarClamp;

    /// <inheritdoc />
    public bool Contains(Vector2 point, Vector2 anchor, Vector2 direction, float bodyRadius) {
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
