using DungeonChessBattle.Game.GamePanels;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.Game.ReplayUI;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 主场景入口脚本，挂载到 MainScene 根节点。
/// 负责服务事件订阅转发、战斗进出路由与屏幕状态机仲裁；
/// 战斗子系统生命周期编排归 BattleCoordinator，数据投影归 BattleSessionContext。
/// </summary>
public partial class MainScene : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<MainScene> _logger = ServiceLocator.GetLogger<MainScene>();

    #region Signals

    /// <summary>战斗结束信号：通知外部监听方（如 UI）战斗编排已退出、回到前厅。</summary>
    [Signal]
    public delegate void BattleEndedEventHandler();

    #endregion

    #region Exports

    [Export]
    private Control? _frontUI;

    [Export]
    private BattleCoordinator? _coordinator;

    /// <summary>回放场景编排器引用，订阅回放启动/结束以仲裁屏幕态。</summary>
    [Export]
    private ReplayCoordinator? _replayCoordinator;

    /// <summary>在线战斗 UI 根（GamePlayUI），供屏幕态机统一仲裁显隐。</summary>
    [Export]
    private Control? _onlineBattleUI;

    #endregion

    #region State

    /// <summary>前端屏幕状态机，统一仲裁 FrontUI 容器显隐与屏幕态。</summary>
    private ScreenStateMachine? _screenMachine;

    #endregion

    /// <summary>
    /// 节点就绪：校验导出引用、订阅战斗信号并挂载战斗完成回调。
    /// </summary>
    public override void _Ready() {
        ValidateExports();

        // 订阅战斗启动事件（服务层事实源，GameLobby 为纯显示层不桥接）
        ServiceLocator.ClientService.OnBattleStarted += OnBattleStarted;
        // 订阅战斗会话终结事件：重连失败或完全断开时退出战斗
        ServiceLocator.ClientService.OnBattleSessionLost += OnBattleSessionLost;

        // 构造屏幕状态机（FrontUI 与在线战斗 UI 显隐统一仲裁）
        _screenMachine = new ScreenStateMachine(_frontUI, _onlineBattleUI);

        // 战斗完成通知：Finished 阶段由 BattleCoordinator 转发，走应用级退出流程
        _coordinator?.OnBattleFinished = ExitBattle;

        // 回放启动/结束仲裁：回放期间隐藏前厅与在线战斗 UI，结束恢复
        _replayCoordinator?.ReplayStarted += OnReplayStarted;
        _replayCoordinator?.ReplayFinished += OnReplayFinished;

        _logger.LogInformation("_Ready Initialized.");
    }

    private void ValidateExports() {
        if (_frontUI == null)
            _logger.LogError("_frontUI is not assigned!");
        if (_coordinator == null)
            _logger.LogError("_coordinator is not assigned!");
    }

    // =============================================================
    // 进入/退出战斗
    // =============================================================

    private void OnBattleStarted(string roomId) {
        // 战斗重连恢复由 BattleCoordinator.EnterBattle 内部处理（先退出旧绑定再重入）
        bool wasInBattle = _coordinator?.IsInBattle ?? false;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Battle started for room: {RoomId}", roomId);

        _coordinator?.EnterBattle(roomId);

        if (!wasInBattle) {
            // 首次进入：隐藏整个前厅 UI 并显示在线战斗 UI（显隐统一由屏幕态机仲裁）
            _screenMachine?.EnterBattle();
        }

        _logger.LogInformation("Entered battle.");
    }

    private void ExitBattle() {
        // 幂等保护：战斗结束回调与会话终结可能先后触发，仅首次生效
        if (_coordinator == null || !_coordinator.IsInBattle)
            return;

        // 主动断开房间连接并清空会话缓存，防止后续房间意外断开时误重连已离开的房间
        ServiceLocator.ClientService.LeaveRoom();

        _coordinator.ExitBattle();

        // 恢复前厅 UI（FrontUI 容器 + 大厅面板）
        _screenMachine?.ExitBattle();

        EmitSignal(SignalName.BattleEnded);
        _logger.LogInformation("Exited battle.");
    }

    /// <summary>战斗会话终结：重连失败或完全断开时退出战斗，避免被困在无响应的战斗画面。</summary>
    private void OnBattleSessionLost() {
        if (_coordinator == null || !_coordinator.IsInBattle)
            return;
        _logger.LogInformation("战斗会话终结，退出战斗。");
        ExitBattle();
    }

    /// <summary>回放启动：经由屏幕态机进入回放态，隐藏前厅与在线战斗 UI，回放 3D 世界复用共享环境与相机。</summary>
    private void OnReplayStarted() {
        _screenMachine?.EnterReplay();
    }

    /// <summary>回放结束：经由屏幕态机退出回放态，恢复前厅，回到进入前的屏幕。</summary>
    private void OnReplayFinished() {
        _screenMachine?.ExitReplay();
    }

    /// <summary>
    /// 节点退出场景树：取消战斗服务事件订阅。
    /// </summary>
    public override void _ExitTree() {
        ServiceLocator.ClientService.OnBattleStarted -= OnBattleStarted;
        ServiceLocator.ClientService.OnBattleSessionLost -= OnBattleSessionLost;
        _replayCoordinator?.ReplayStarted -= OnReplayStarted;
        _replayCoordinator?.ReplayFinished -= OnReplayFinished;
    }
}
