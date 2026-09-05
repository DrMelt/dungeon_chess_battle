using DungeonChessBattle.Game.BattleScene;
using DungeonChessBattle.Game.GamePanels;
using DungeonChessBattle.Game.ReplayUI;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.Replay.Shared;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 主场景入口脚本，挂载到 MainScene 根节点。
/// 负责服务事件订阅转发、屏幕状态机仲裁，以及战斗/回放两套完整组装场景的互斥加载与释放：
/// 进入战斗实例化 battle 组装场景（根即 BattleCoordinator），启动回放实例化 replay 组装场景
/// （根即 ReplayCoordinator，自带回放表现），退出即释放，同一时刻至多存在一套。
/// 战斗子系统生命周期编排归 BattleCoordinator，回放引擎生命周期归 ReplayCoordinator，本节点不碰其内部。
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

    /// <summary>在线战斗组装场景，根节点为 BattleCoordinator，OnBattleStarted 时实例化。</summary>
    [Export]
    private PackedScene? _battleAssembleScene;

    /// <summary>回放组装场景，根节点为 ReplayCoordinator（自带回放表现），StartReplay 时实例化。</summary>
    [Export]
    private PackedScene? _replayAssembleScene;

    #endregion

    #region State

    /// <summary>前端屏幕状态机，统一仲裁 FrontUI 容器显隐与屏幕态。</summary>
    private ScreenStateMachine? _screenMachine;

    /// <summary>在场战斗组装场景，未在战斗中为 null。</summary>
    private BattleCoordinator? _battleCoordinator;

    /// <summary>在场回放组装场景，未在回放中为 null。</summary>
    private ReplayCoordinator? _replayCoordinator;

    #endregion

    /// <summary>
    /// 节点就绪：装配 mod 内容、校验导出引用、订阅战斗服务事件、构造屏幕状态机。
    /// mod 装配必须先于任何面板与资源表访问，主场景是最早的装配时机。
    /// </summary>
    public override void _Ready() {
        ModManager.EnsureInitialized();
        ValidateExports();

        // 订阅战斗启动事件（服务层事实源，GameLobby 为纯显示层不桥接）
        ServiceLocator.ClientService.OnBattleStarted += OnBattleStarted;
        // 订阅战斗会话终结事件：重连失败或完全断开时退出战斗
        ServiceLocator.ClientService.OnBattleSessionLost += OnBattleSessionLost;

        _screenMachine = new ScreenStateMachine(_frontUI);

        _logger.LogInformation("_Ready Initialized.");
    }

    private void ValidateExports() {
        if (_frontUI == null)
            _logger.LogError("_frontUI is not assigned!");
        if (_battleAssembleScene == null)
            _logger.LogError("_battleAssembleScene is not assigned!");
        if (_replayAssembleScene == null)
            _logger.LogError("_replayAssembleScene is not assigned!");
    }

    // =============================================================
    // 进入/退出战斗
    // =============================================================

    /// <summary>进入战斗：加载战斗组装场景并驱动其编排器，首次进入交屏幕态机切战斗态。</summary>
    private void OnBattleStarted(string roomId) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Battle started for room: {RoomId}", roomId);

        bool wasInBattle = _battleCoordinator?.IsInBattle ?? false;
        var coordinator = EnsureBattleAssembly();
        if (coordinator == null)
            return;

        // 重连恢复由 BattleCoordinator.EnterBattle 内部处理（先退出旧绑定再重入）
        coordinator.EnterBattle(roomId);
        if (!wasInBattle)
            _screenMachine?.EnterBattle();

        _logger.LogInformation("Entered battle.");
    }

    /// <summary>战斗组装场景在场则复用，缺席即实例化并挂到本节点下。</summary>
    private BattleCoordinator? EnsureBattleAssembly() {
        if (_battleCoordinator != null)
            return _battleCoordinator;
        if (_battleAssembleScene == null)
            return null;

        var assemble = _battleAssembleScene.Instantiate<BattleCoordinator>();
        AddChild(assemble);
        assemble.OnBattleFinished = ExitBattle;
        _battleCoordinator = assemble;
        return assemble;
    }

    /// <summary>退出战斗：解绑编排器、释放战斗组装场景并恢复前厅。</summary>
    private void ExitBattle() {
        var coordinator = _battleCoordinator;
        // 幂等保护：战斗结束回调与会话终结可能先后触发，仅首次生效
        if (coordinator == null || !coordinator.IsInBattle)
            return;

        // 主动断开房间连接并清空会话缓存，防止后续房间意外断开时误重连已离开的房间
        ServiceLocator.ClientService.LeaveRoom();

        coordinator.ExitBattle();
        _battleCoordinator = null;
        coordinator.QueueFree();

        // 恢复前厅 UI（FrontUI 容器 + 大厅面板）
        _screenMachine?.ExitBattle();

        EmitSignal(SignalName.BattleEnded);
        _logger.LogInformation("Exited battle.");
    }

    /// <summary>战斗会话终结：重连失败或完全断开时退出战斗，避免被困在无响应的战斗画面。</summary>
    private void OnBattleSessionLost() {
        if (_battleCoordinator == null || !_battleCoordinator.IsInBattle)
            return;
        _logger.LogInformation("战斗会话终结，退出战斗。");
        ExitBattle();
    }

    // =============================================================
    // 回放装配
    // =============================================================

    /// <summary>
    /// 启动回放：实例化回放组装场景并加载记录，成功返回 true 供入口面板决定导航去向。
    /// 引擎构建失败即回收场景，不留半启动态。
    /// </summary>
    public bool StartReplay(ReplayRecording recording) {
        if (_replayCoordinator != null) {
            _logger.LogWarning("回放已在进行中，忽略重复启动。");
            return false;
        }
        if (_replayAssembleScene == null)
            return false;

        var replay = _replayAssembleScene.Instantiate<ReplayCoordinator>();
        AddChild(replay);
        replay.ReplayStarted += OnReplayStarted;
        replay.ReplayFinished += OnReplayFinished;
        replay.LoadReplay(recording);
        if (!replay.IsActive) {
            replay.QueueFree();
            return false;
        }

        _replayCoordinator = replay;
        return true;
    }

    /// <summary>回放启动：经屏幕态机进入回放态，隐藏前厅。</summary>
    private void OnReplayStarted() => _screenMachine?.EnterReplay();

    /// <summary>回放结束：释放回放组装场景并恢复前厅。</summary>
    private void OnReplayFinished() {
        var replay = _replayCoordinator;
        if (replay == null)
            return;

        _replayCoordinator = null;
        replay.ReplayStarted -= OnReplayStarted;
        replay.ReplayFinished -= OnReplayFinished;
        replay.QueueFree();
        _screenMachine?.ExitReplay();
    }

    /// <summary>
    /// 节点退出场景树：取消战斗服务事件订阅。
    /// </summary>
    public override void _ExitTree() {
        ServiceLocator.ClientService.OnBattleStarted -= OnBattleStarted;
        ServiceLocator.ClientService.OnBattleSessionLost -= OnBattleSessionLost;
    }
}
