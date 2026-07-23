using System.Numerics;

namespace DungeonChessBattle.Core {
    public static class VectorMath {
        /// <summary>
        /// 等价于 Godot Vector2.Cross 的标量结果（叉积 Z 分量），
        /// 用于 2D 向量夹角方向判断。
        /// </summary>
        public static float Cross(Vector2 a, Vector2 b) {
            return a.X * b.Y - a.Y * b.X;
        }
    }
}
