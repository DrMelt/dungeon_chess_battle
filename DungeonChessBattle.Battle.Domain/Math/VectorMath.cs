using System.Numerics;

namespace DungeonChessBattle.Battle.Domain.Math;

/// <summary>2D 向量数学工具。</summary>
public static class VectorMath {
    /// <summary>叉积 Z 分量，用于 2D 向量夹角方向判断。</summary>
    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>
    /// 两点间线性插值（对应 LES InterpolatorDelegateWithReturn&lt;Vector2&gt; 签名）。
    /// 用于网络同步字段类型（SyncVar&lt;Vector2&gt;）的插值注册。
    /// </summary>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + (b - a) * t;
}
