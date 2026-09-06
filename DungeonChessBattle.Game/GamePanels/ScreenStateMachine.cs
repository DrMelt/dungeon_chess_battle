using Godot;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// 前端屏幕状态机：统一管理 主菜单/大厅/房间准备/战斗/回放 屏幕态切换。
/// 只仲裁前厅图层（FrontUI 容器）的显隐与屏幕态枚举，面板间导航仍由 BaseGamePanel 的 caller 链负责。
/// 战斗与回放表现为两套互斥加载的组装场景，其显隐随场景加载/释放天然成立，不经本状态机。
/// </summary>
/// <param name="frontUI">前厅 UI 容器引用（Interface/FrontUI）。</param>
public sealed class ScreenStateMachine(Control? frontUI) {
    /// <summary>前厅 UI 容器（FrontUI），进入战斗/回放时整体隐藏。</summary>
    private readonly Control _frontUI = frontUI ?? throw new System.ArgumentNullException(nameof(frontUI));

    /// <summary>当前屏幕状态。</summary>
    public GameScreenState Current {
        get; private set;
    } = GameScreenState.MainMenu;

    /// <summary>屏幕状态变化事件。</summary>
    public event System.Action<GameScreenState>? StateChanged;

    /// <summary>
    /// 进入战斗：切换屏幕态、隐藏整个 FrontUI（含全屏背景 Panel 与所有前厅面板）。
    /// </summary>
    public void EnterBattle() {
        TransitionTo(GameScreenState.Battle);
        _frontUI.Visible = false;
    }

    /// <summary>
    /// 进入回放：切换屏幕态并隐藏 FrontUI（回放表现随回放组装场景自身呈现）。
    /// </summary>
    public void EnterReplay() {
        TransitionTo(GameScreenState.Replay);
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

    /// <summary>
    /// 退出回放：行为与退出战斗一致（恢复 FrontUI 并回到大厅），复用同一实现。
    /// 回放仅从大厅进入，恢复后大厅即在原位。
    /// </summary>
    public void ExitReplay() => ExitBattle();

    private void TransitionTo(GameScreenState next) {
        Current = next;
        StateChanged?.Invoke(next);
    }
}
