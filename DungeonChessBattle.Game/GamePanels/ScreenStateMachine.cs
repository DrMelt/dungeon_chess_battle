using Godot;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// 前端屏幕状态机：统一管理 主菜单/大厅/房间准备/战斗/回放 屏幕态切换。
/// 只仲裁顶层图层（FrontUI 容器、在线战斗 UI）的显隐与屏幕态枚举，
/// 面板间导航仍由 BaseGamePanel 的 caller 链负责，避免职责重叠。
/// 进入战斗隐藏整个 FrontUI 并显示在线战斗 UI；进入回放隐藏 FrontUI 与在线战斗 UI；
/// 退出恢复 FrontUI 并隐藏在线战斗 UI。
/// </summary>
/// <param name="frontUI">前厅 UI 容器引用（Interface/FrontUI）。</param>
/// <param name="onlineBattleUI">在线战斗 UI 根（GamePlayUI），回放期间隐藏。</param>
public sealed class ScreenStateMachine(Control? frontUI, Control? onlineBattleUI) {
    /// <summary>前厅 UI 容器（FrontUI），进入战斗/回放时整体隐藏。</summary>
    private readonly Control _frontUI = frontUI ?? throw new System.ArgumentNullException(nameof(frontUI));

    /// <summary>在线战斗 UI 根（GamePlayUI），进入回放时隐藏。</summary>
    private readonly Control? _onlineBattleUI = onlineBattleUI;

    /// <summary>当前屏幕状态。</summary>
    public GameScreenState Current {
        get; private set;
    } = GameScreenState.MainMenu;

    /// <summary>屏幕状态变化事件。</summary>
    public event System.Action<GameScreenState>? StateChanged;

    /// <summary>
    /// 进入战斗：切换屏幕态、隐藏整个 FrontUI（含全屏背景 Panel 与所有前厅面板）并显示在线战斗 UI。
    /// </summary>
    public void EnterBattle() {
        TransitionTo(GameScreenState.Battle);
        _frontUI.Visible = false;
        SetOnlineBattleUI(true);
    }

    /// <summary>
    /// 进入回放：切换屏幕态、隐藏 FrontUI 与在线战斗 UI（回放控制条由回放场景自管）。
    /// </summary>
    public void EnterReplay() {
        TransitionTo(GameScreenState.Replay);
        _frontUI.Visible = false;
        SetOnlineBattleUI(false);
    }

    /// <summary>
    /// 退出战斗：恢复 FrontUI、隐藏在线战斗 UI 并复位到大厅。
    /// RoomPreparation 进入战斗前已自行隐藏，此处仅恢复大厅面板。
    /// </summary>
    public void ExitBattle() {
        _frontUI.Visible = true;
        SetOnlineBattleUI(false);
        TransitionTo(GameScreenState.Lobby);
    }

    /// <summary>
    /// 退出回放：恢复 FrontUI、隐藏在线战斗 UI 并回到主菜单（当前回放仅从主菜单进入）。
    /// </summary>
    public void ExitReplay() {
        _frontUI.Visible = true;
        SetOnlineBattleUI(false);
        TransitionTo(GameScreenState.MainMenu);
    }

    /// <summary>设置在线战斗 UI 显隐。</summary>
    private void SetOnlineBattleUI(bool visible) {
        _onlineBattleUI?.Visible = visible;
    }

    private void TransitionTo(GameScreenState next) {
        Current = next;
        StateChanged?.Invoke(next);
    }
}
