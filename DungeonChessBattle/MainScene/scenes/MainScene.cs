using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Client.Battle;
using BattlePhase = DungeonChessBattle.Battle.Domain.Combat.BattlePhase;
using DungeonChessBattle.GamePanels;
using DungeonChessBattle.GamePlayUI;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 主场景入口脚本，挂载到 MainScene 根节点。
/// 负责服务装配与战斗生命周期编排（进入/退出/结束检测）。
/// 帧循环委托子组件：输入采集（BattleInputController）、单位同步（BattleUnitManager）。
/// 全部通过 IClientBattleService 接口消费服务，不再依赖 RoomBattleClient 具体类型。
/// </summary>
public partial class MainScene : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<MainScene> _logger = ServiceLocator.GetLogger<MainScene>();

    #region Signals

    /// <summary>战斗结束信号。</summary>
    [Signal]
    public delegate void BattleEndedEventHandler();

    #endregion

    #region Exports

    [Export]
    private Control? _frontUI;

    [Export]
    private BattleUIRoot? _battleUiRoot;

    [Export]
    private BattleUnitManager? _unitManager;

    [Export]
    private BattleInputController? _inputController;

    #endregion

    #region State

    /// <summary>战斗服务（接口类型，通过 ServiceLocator 获取）。唯一服务引用。</summary>
    private IClientBattleService? _battleService;

    /// <summary>前端屏幕状态机，统一仲裁 FrontUI 容器显隐与屏幕态。</summary>
    private ScreenStateMachine? _screenMachine;

    private string _roomId = "";
    private bool _inBattle;

    #endregion

    /// <summary>
    /// 节点就绪：校验导出引用、订阅战斗开始信号并显示主菜单。
    /// </summary>
    public override void _Ready() {
        ValidateExports();

        // 订阅战斗启动事件（服务层事实源，GameLobby 为纯显示层不桥接）
        ServiceLocator.ClientService.OnBattleStarted += OnBattleStarted;

        // 构造屏幕状态机（FrontUI 容器在战斗中整体隐藏）
        _screenMachine = new ScreenStateMachine(_frontUI);

        _logger.LogInformation("_Ready Initialized.");
    }

    private void ValidateExports() {
        if (_frontUI == null)
            _logger.LogError("_frontUI is not assigned!");
        if (_battleUiRoot == null)
            _logger.LogError("_battleUiRoot is not assigned!");
        if (_unitManager == null)
            _logger.LogError("_unitManager is not assigned!");
        if (_inputController == null)
            _logger.LogError("_inputController is not assigned!");
    }

    // =============================================================
    // 进入/退出战斗
    // =============================================================

    private void OnBattleStarted(string roomId) {
        _roomId = roomId;
        _battleService = ServiceLocator.ClientService.RoomClient;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Battle started for room: {RoomId}", roomId);

        // 生命周期事件（战斗阶段）由 MainScene 持有；单位事件归 BattleUnitManager
        if (_battleService != null) {
            _battleService.BattlePhaseChanged += OnBattlePhase;

            _unitManager?.Bind(_battleService, ServiceLocator.ClientService.RoomClient, roomId);
            _inputController?.Reset();
        }

        // 绑定战斗 UI（View 层直接初始化子组件）
        _battleUiRoot?.BindToBattle();

        // 隐藏整个前厅 UI（FrontUI + 全屏背景 Panel）
        _screenMachine?.EnterBattle();
        _inBattle = true;

        _logger.LogInformation("Entered battle.");
    }

    private void ExitBattle() {
        // 主动断开房间连接并清空会话缓存，防止后续房间意外断开时误重连已离开的房间
        ServiceLocator.ClientService.LeaveRoom();

        _battleService?.BattlePhaseChanged -= OnBattlePhase;

        // 子组件退订与清理
        _unitManager?.Unbind();
        _inputController?.Reset();

        // 解绑战斗 UI
        _battleUiRoot?.UnbindFromBattle();

        _battleService = null;
        _roomId = "";
        _inBattle = false;

        // 恢复前厅 UI（FrontUI 容器 + 大厅面板）
        _screenMachine?.ExitBattle();

        EmitSignal(SignalName.BattleEnded);
        _logger.LogInformation("Exited battle.");
    }

    // =============================================================
    // 帧循环：委托子组件
    // =============================================================

    /// <summary>
    /// 每帧处理战斗输入收集并提交到战斗服务。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (!_inBattle || _battleService == null)
            return;

        _inputController?.Tick(_battleService);
    }

    // =============================================================
    // 战斗阶段
    // =============================================================

    private void OnBattlePhase(string roomId, BattlePhase phase) {
        if (roomId != _roomId)
            return;
        CallDeferred(nameof(DeferredBattlePhase), (int)phase);
    }

    private void DeferredBattlePhase(int phase) {
        if (phase == (int)BattlePhase.Finished) {
            _logger.LogInformation("Battle finished detected via LES sync.");
            CallDeferred(nameof(ExitBattle));
        }
    }

    /// <summary>
    /// 节点退出场景树：取消战斗服务事件订阅。
    /// </summary>
    public override void _ExitTree() {
        ServiceLocator.ClientService.OnBattleStarted -= OnBattleStarted;
        _battleService?.BattlePhaseChanged -= OnBattlePhase;
    }
}
