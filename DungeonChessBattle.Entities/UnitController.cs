using System.Numerics;
using LiteEntitySystem;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 单位的人工输入控制器。由 Godot UI 层每帧注入输入，框架自动发送到服务端。
/// </summary>
public class UnitController : HumanControllerLogic<UnitInputPacket, UnitPawn> {
    private UnitInputPacket _latestInput;

    public UnitController(EntityParams entityParams) : base(entityParams) { }

    /// <summary>
    /// Godot UI 层调用，提交当前帧的输入。
    /// </summary>
    public void SubmitInput(Vector2 moveDir, byte skillFlags, Vector2 aimPos) {
        _latestInput = UnitInputPacket.Create(
            moveDir,
            skillFlags,
            aimPos,
            (ushort)(Environment.TickCount & 0xFFFF));
    }

    protected override UnitInputPacket GetDefaultInput() {
        return _latestInput;
    }
}
