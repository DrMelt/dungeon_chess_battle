using System.Numerics;
using System.Runtime.InteropServices;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 单位单帧输入的扁平化结构，用于网络传输。
/// 采用顺序布局以支持非托管传输。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct UnitInputPacket {
    /// <summary>移动方向 X 分量。</summary>
    public float MoveX;

    /// <summary>移动方向 Y 分量。</summary>
    public float MoveY;

    /// <summary>技能按压位标志，bit i 表示第 i 个技能被按下。</summary>
    public byte SkillFlags;

    /// <summary>瞄准位置 X 分量。</summary>
    public float AimX;

    /// <summary>瞄准位置 Y 分量。</summary>
    public float AimY;

    /// <summary>输入时间戳，环境 TickCount 低 16 位。</summary>
    public ushort Timestamp;

    /// <summary>移动方向向量。</summary>
    public readonly Vector2 MoveDirection => new(MoveX, MoveY);

    /// <summary>瞄准位置向量。</summary>
    public readonly Vector2 AimPosition => new(AimX, AimY);

    /// <summary>
    /// 判断指定技能是否被按下。
    /// </summary>
    /// <param name="skillIndex">技能索引，从 0 开始。</param>
    /// <returns>技能被按下返回 true。</returns>
    public readonly bool IsSkillPressed(int skillIndex) {
        return (SkillFlags & (1 << skillIndex)) != 0;
    }

    /// <summary>
    /// 创建一组输入数据。
    /// </summary>
    /// <param name="moveDir">移动方向向量。</param>
    /// <param name="skillFlags">技能按压位标志。</param>
    /// <param name="aimPos">瞄准位置。</param>
    /// <param name="timestamp">输入时间戳。</param>
    /// <returns>打包后的 <see cref="UnitInputPacket"/>。</returns>
    public static UnitInputPacket Create(Vector2 moveDir, byte skillFlags, Vector2 aimPos, ushort timestamp) {
        return new UnitInputPacket {
            MoveX = moveDir.X,
            MoveY = moveDir.Y,
            SkillFlags = skillFlags,
            AimX = aimPos.X,
            AimY = aimPos.Y,
            Timestamp = timestamp,
        };
    }
}
