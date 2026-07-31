using System.Numerics;
using System.Runtime.InteropServices;

namespace DungeonChessBattle.Entities;

[StructLayout(LayoutKind.Sequential)]
public struct UnitInputPacket {
    public float MoveX;
    public float MoveY;
    public byte SkillFlags;
    public float AimX;
    public float AimY;
    public ushort Timestamp;

    public readonly Vector2 MoveDirection => new(MoveX, MoveY);
    public readonly Vector2 AimPosition => new(AimX, AimY);

    public readonly bool IsSkillPressed(int skillIndex) {
        return (SkillFlags & (1 << skillIndex)) != 0;
    }

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
