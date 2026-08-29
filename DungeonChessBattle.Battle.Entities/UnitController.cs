using System.Numerics;
using DungeonChessBattle.Battle.Entities.Requests;
using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// 单位的人工输入控制器。由 Godot UI 层每帧注入输入，框架自动发送到服务端。
/// 客户端侧：SubmitInput 写入待发送缓冲并发往服务端，Send*Request 走可靠请求通道；
/// 服务端侧：BeforeControlledUpdate 每逻辑 tick 读取 CurrentInput 并转发给受控单位，
/// BindServer*Handler 订阅客户端事件请求并注入房间权威校验。
/// </summary>
public class UnitController : HumanControllerLogic<UnitInputPacket, UnitPawn> {
    private UnitInputPacket _latestInput;

    /// <summary>服务端：施放技能请求处理委托，由房间编排注入权威校验。</summary>
    private Func<CastSkillRequest, bool>? _castHandler;

    /// <summary>服务端：聚焦目标设置请求处理委托，由房间编排注入权威校验。</summary>
    private Func<SetFocusTargetRequest, bool>? _focusHandler;

    /// <summary>
    /// 初始化单位控制器。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public UnitController(EntityParams entityParams) : base(entityParams) { }

    /// <summary>
    /// 客户端调用：经可靠请求通道发送施法请求，服务端权威读条与结算。
    /// 一次性事件，不复用于持续的输入流。
    /// </summary>
    /// <param name="request">施法请求载荷。</param>
    /// <param name="onResult">服务端回执回调，true 表示施法已接受。</param>
    public void SendCastSkillRequest(CastSkillRequest request, Action<bool>? onResult = null) {
        if (onResult == null)
            SendRequestStruct(request);
        else
            SendRequestStruct(request, onResult);
    }

    /// <summary>
    /// 客户端调用：经可靠请求通道发送聚焦目标设置请求，服务端校验后写回权威状态。
    /// </summary>
    /// <param name="request">聚焦目标请求载荷。</param>
    /// <param name="onResult">服务端回执回调，true 表示目标已接受。</param>
    public void SendSetFocusTargetRequest(SetFocusTargetRequest request, Action<bool>? onResult = null) {
        if (onResult == null)
            SendRequestStruct(request);
        else
            SendRequestStruct(request, onResult);
    }

    /// <summary>
    /// 服务端调用：绑定施法请求处理。请求到达时框架自动把返回值作为回执发回客户端。
    /// 仅在服务端注册订阅，客户端实例不接收请求。
    /// </summary>
    /// <param name="handler">返回 true 表示接受施放。</param>
    public void BindServerCastHandler(Func<CastSkillRequest, bool> handler) {
        _castHandler = handler;
        if (IsServer)
            SubscribeToClientRequestStruct<CastSkillRequest>(OnCastSkillRequest);
    }

    /// <summary>
    /// 服务端调用：绑定聚焦目标请求处理，仅服务端生效。
    /// </summary>
    /// <param name="handler">返回 true 表示目标已接受。</param>
    public void BindServerFocusHandler(Func<SetFocusTargetRequest, bool> handler) {
        _focusHandler = handler;
        if (IsServer)
            SubscribeToClientRequestStruct<SetFocusTargetRequest>(OnSetFocusTargetRequest);
    }

    private bool OnCastSkillRequest(CastSkillRequest req) => _castHandler?.Invoke(req) ?? false;

    private bool OnSetFocusTargetRequest(SetFocusTargetRequest req) => _focusHandler?.Invoke(req) ?? false;

    /// <summary>
    /// Godot UI 层调用，提交当前帧的移动输入。
    /// 内部通过 <c>ModifyPendingInput()</c> 写入待发送缓冲，随下一网络包发往服务端。
    /// </summary>
    /// <param name="moveDir">移动方向向量。</param>
    public void SubmitInput(Vector2 moveDir) {
        _latestInput = UnitInputPacket.Create(moveDir);

        ref var pending = ref ModifyPendingInput();
        pending = _latestInput;
    }

    /// <summary>
    /// 服务端每逻辑 tick 调用：把服务端收到的玩家输入转给受控单位，由单位领域逻辑消费。
    /// </summary>
    protected override void BeforeControlledUpdate() {
        base.BeforeControlledUpdate();
        if (ControlledEntity == null)
            return;

        // 输入经 ServerApplyInput 转发到领域层消费：移动打断读条等在 Logic 层。
        // 实体层不再持有移动输入，位移由领域 BattleScene 统一结算。
        var input = CurrentInput;
        if (EntityManager.IsServer)
            ControlledEntity.ServerApplyInput(input, EntityManager.DeltaTimeF);
    }

    /// <summary>
    /// 返回默认输入包：当前最新一帧的输入。
    /// </summary>
    /// <returns>最新提交的输入包。</returns>
    protected override UnitInputPacket GetDefaultInput() {
        return _latestInput;
    }
}
