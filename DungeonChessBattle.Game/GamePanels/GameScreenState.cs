namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// 游戏屏幕状态（前端 UI 整体所处阶段）。
/// 由 ScreenStateMachine 统一维护，作为屏幕阶段的单一事实源。
/// </summary>
public enum GameScreenState : byte {
    /// <summary>主菜单。</summary>
    MainMenu,
    /// <summary>游戏大厅（招募板）。</summary>
    Lobby,
    /// <summary>房间准备。</summary>
    RoomPrep,
    /// <summary>战斗中（FrontUI 整体隐藏）。</summary>
    Battle,
    /// <summary>回放中（FrontUI 与在线战斗 UI 均隐藏，回放控制条由回放场景自管）。</summary>
    Replay,
}
