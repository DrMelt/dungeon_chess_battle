using Godot;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// 前端屏幕状态机：统一管理 主菜单/大厅/房间准备/战斗 四态切换。
/// 只仲裁 FrontUI 容器（含全屏背景 Panel）的显隐与屏幕态枚举，
/// 面板间导航仍由 BaseGamePanel 的 caller 链负责，避免职责重叠。
/// 进入战斗隐藏整个 FrontUI；退出战斗恢复 FrontUI 并复位到大厅。
/// </summary>
/// <remarks>
/// 初始化状态机。
/// </remarks>
/// <param name="frontUI">前厅 UI 容器引用（Interface/FrontUI）。</param>
public sealed class ScreenStateMachine(Control? frontUI) {
    /// <summary>前厅 UI 容器（FrontUI），进入战斗时整体隐藏。</summary>
    private readonly Control _frontUI = frontUI ?? throw new System.ArgumentNullException(nameof(frontUI));

    /// <summary>当前屏幕状态。</summary>
    public GameScreenState Current {
        get; private set;
    } = GameScreenState.MainMenu;

    /// <summary>屏幕状态变化事件。</summary>
    public event System.Action<GameScreenState>? StateChanged;

    /// <summary>
    /// 进入战斗：切换屏幕态并隐藏整个 FrontUI（含全屏背景 Panel 与所有前厅面板）。
    /// </summary>
    public void EnterBattle() {
        TransitionTo(GameScreenState.Battle);
        _frontUI.Visible = false;
    }

    /// <summary>
    /// 退出战斗：恢复 FrontUI 并复位到大厅。
    /// RoomPreparation 进入战斗前已自行隐藏，此处仅恢复大厅面板。
    /// </summary>
    public void ExitBattle() {
        _frontUI.Visible = true;

        TransitionTo(GameScreenState.Lobby);
    }

    private void TransitionTo(GameScreenState next) {
        Current = next;
        StateChanged?.Invoke(next);
    }
}
