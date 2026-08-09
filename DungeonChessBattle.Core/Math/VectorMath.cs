using System.Numerics;

namespace DungeonChessBattle.Core.Math;

/// <summary>
/// 2D 向量数学工具。
/// </summary>
public static class VectorMath {
    /// <summary>
    /// 等价于 Godot Vector2.Cross 的标量结果（叉积 Z 分量），
    /// 用于 2D 向量夹角方向判断。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <returns>a 与 b 的叉积标量值。</returns>
    public static float Cross(Vector2 a, Vector2 b) {
        return a.X * b.Y - a.Y * b.X;
    }

    /// <summary>
    /// 两点间线性插值（对应 LES InterpolatorDelegateWithReturn&lt;Vector2&gt; 签名）。
    /// 用于网络同步字段类型（SyncVar&lt;Vector2&gt;）的插值注册。
    /// </summary>
    /// <param name="a">起点。</param>
    /// <param name="b">终点。</param>
    /// <param name="t">插值系数（0~1）。</param>
    /// <returns>插值结果。</returns>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) {
        return a + (b - a) * t;
    }
}
