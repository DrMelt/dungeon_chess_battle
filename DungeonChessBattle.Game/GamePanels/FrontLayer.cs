using Godot;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// 前厅图层仲裁：统一收发前厅 UI 容器（FrontUI）的显隐。
/// 进战斗与进回放整体隐藏，退出即恢复；面板间导航仍由 BaseGamePanel 的 caller 链负责。
/// 战斗与回放表现为两套互斥加载的组装场景，其显隐随场景加载/释放天然成立，不经本类。
/// </summary>
/// <param name="frontUI">前厅 UI 容器引用（Interface/FrontUI）。</param>
public sealed class FrontLayer(Control? frontUI) {
    /// <summary>前厅 UI 容器（FrontUI），进入战斗/回放时整体隐藏。</summary>
    private readonly Control _frontUI = frontUI ?? throw new System.ArgumentNullException(nameof(frontUI));

    /// <summary>进入战斗：隐藏整个 FrontUI（含全屏背景 Panel 与所有前厅面板）。</summary>
    public void EnterBattle() => _frontUI.Visible = false;

    /// <summary>进入回放：与进入战斗同口径，回放表现随回放组装场景自身呈现。</summary>
    public void EnterReplay() => EnterBattle();

    /// <summary>退出战斗：恢复 FrontUI。RoomPreparation 进入战斗前已自行隐藏，此处仅恢复大厅面板。</summary>
    public void ExitBattle() => _frontUI.Visible = true;

    /// <summary>退出回放：与退出战斗同口径。回放仅从大厅进入，恢复后大厅即在原位。</summary>
    public void ExitReplay() => ExitBattle();
}
