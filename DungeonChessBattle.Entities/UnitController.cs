using System.Numerics;
using LiteEntitySystem;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 单位的人工输入控制器。由 Godot UI 层每帧注入输入，框架自动发送到服务端。
/// 客户端侧：SubmitInput 写入待发送缓冲并发往服务端；
/// 服务端侧：BeforeControlledUpdate 每逻辑 tick 读取 CurrentInput 并转发给受控单位。
/// </summary>
public class UnitController : HumanControllerLogic<UnitInputPacket, UnitPawn> {
    private UnitInputPacket _latestInput;

    /// <summary>
    /// 初始化单位控制器。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public UnitController(EntityParams entityParams) : base(entityParams) { }

    /// <summary>
    /// Godot UI 层调用，提交当前帧的输入。
    /// 内部通过 <c>ModifyPendingInput()</c> 写入待发送缓冲，随下一网络包发往服务端。
    /// </summary>
    /// <param name="moveDir">移动方向向量。</param>
    /// <param name="skillFlags">技能按压位标志（bit i 表示第 i 个技能被按下）。</param>
    /// <param name="aimPos">瞄准位置。</param>
    public void SubmitInput(Vector2 moveDir, byte skillFlags, Vector2 aimPos) {
        _latestInput = UnitInputPacket.Create(
            moveDir,
            skillFlags,
            aimPos,
            (ushort)(Environment.TickCount & 0xFFFF));

        ref var pending = ref ModifyPendingInput();
        pending = _latestInput;
    }

    /// <summary>
    /// 服务端每逻辑 tick 调用：把服务端收到的玩家输入转给受控单位，由单位领域逻辑消费。
    /// </summary>
    protected override void BeforeControlledUpdate() {
        base.BeforeControlledUpdate();
        if (!EntityManager.IsServer || ControlledEntity == null)
            return;
        ControlledEntity.ServerApplyInput(CurrentInput, EntityManager.DeltaTimeF);
    }

    /// <summary>
    /// 返回默认输入包：当前最新一帧的输入。
    /// </summary>
    /// <returns>最新提交的输入包。</returns>
    protected override UnitInputPacket GetDefaultInput() {
        return _latestInput;
    }
}
