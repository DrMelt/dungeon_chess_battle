using DungeonChessBattle.Entities;
using DungeonChessBattle.MainScene;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 迷你状态条组件，展示单个单位的 Buff、血条与施法进度。
/// 鼠标悬停时显示高亮外框，点击时请求选中绑定单位。
/// </summary>
public partial class ClickableStateBar : Control {

    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ClickableStateBar> _logger = ServiceLocator.GetLogger<ClickableStateBar>();

    /// <summary>导出引用集合节点。</summary>
    public ClickableStateBarInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>战斗会话上下文引用，点击状态条时经其请求选中绑定单位。</summary>
    public BattleSessionContext? SessionRef {
        get; set;
    }

    /// <summary>鼠标是否悬停在该状态条上。</summary>
    private bool mouseOn = false;

    /// <summary>当前绑定的单位 Pawn。</summary>
    private UnitPawn? bindingPawn;

    /// <summary>
    /// 节点就绪：获取引用集合，并监听鼠标悬停与左键点击事件。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<ClickableStateBarInterRefs>("ClickableStateBarInterRefs");
        MouseEntered += () => {
            mouseOn = true;
            if (InterRefs?.OutlineRef != null)
                InterRefs.OutlineRef.Visible = true;
        };
        MouseExited += () => {
            mouseOn = false;
            if (InterRefs?.OutlineRef != null)
                InterRefs.OutlineRef.Visible = false;
        };
        GuiInput += OnGuiInput;
    }

    /// <summary>
    /// 处理鼠标左键点击，请求选中当前绑定的单位并消费事件。
    /// </summary>
    /// <param name="event">输入事件。</param>
    private void OnGuiInput(InputEvent @event) {
        if (@event is InputEventMouseButton mouseBtn
            && mouseBtn.ButtonIndex == MouseButton.Left
            && mouseBtn.Pressed
            && bindingPawn != null) {
            if (SessionRef == null)
                _logger.LogWarning("SessionRef is not assigned!");
            else
                SessionRef.SetLocalFocusTarget(bindingPawn.Id);
            AcceptEvent();
        }
    }

    /// <summary>
    /// 绑定要展示的单位 Pawn。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    public void BindUnitState(UnitPawn pawn) {
        bindingPawn = pawn;
    }

    /// <summary>
    /// 每帧刷新子组件的单位 Pawn 展示。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (InterRefs == null || bindingPawn == null)
            return;
        InterRefs.ContainerBuffsRef?.UpdateUI_WithUnit(bindingPawn);
        InterRefs.HpStateBarRef?.UpdateUI_WithUnit(bindingPawn, SessionRef);
        InterRefs.SkillProgressBarRef?.UpdateUI_WithUnit(bindingPawn);
    }
}
