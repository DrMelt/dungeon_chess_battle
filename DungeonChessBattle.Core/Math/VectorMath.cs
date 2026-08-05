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
}
