using System.Numerics;
using System.Runtime.InteropServices;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 单位单帧输入的扁平化结构，用于网络传输。
/// 输入流仅承载移动状态；技能等一次性事件经 UnitController 可靠请求通道送达。
/// 采用顺序布局以支持非托管传输。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct UnitInputPacket {
    /// <summary>移动方向 X 分量。</summary>
    public float MoveX;

    /// <summary>移动方向 Y 分量。</summary>
    public float MoveY;

    /// <summary>移动方向向量。</summary>
    public readonly Vector2 MoveDirection => new(MoveX, MoveY);

    /// <summary>
    /// 创建一组输入数据。
    /// </summary>
    /// <param name="moveDir">移动方向向量。</param>
    /// <returns>打包后的 <see cref="UnitInputPacket"/>。</returns>
    public static UnitInputPacket Create(Vector2 moveDir) {
        return new UnitInputPacket {
            MoveX = moveDir.X,
            MoveY = moveDir.Y,
        };
    }
}
