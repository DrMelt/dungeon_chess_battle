using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.GamePanels;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Services;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 主场景入口脚本，挂载到 MainScene 根节点。
/// 负责服务装配与战斗生命周期编排（进入/退出/结束检测）。
/// 帧循环委托子组件：输入采集（BattleInputController）、单位同步（BattleUnitManager）。
/// 全部通过 IClientBattleService 接口消费服务，不再依赖 RoomBattleClient 具体类型。
/// </summary>
public partial class MainScene : Node {
    #region Signals

    /// <summary>战斗结束信号。</summary>
    [Signal]
    public delegate void BattleEndedEventHandler();

    #endregion

    #region Exports

    [Export]
    private MainMenu? _mainMenu;

    [Export]
    private GameLobby? _gameLobby;

    [Export]
    private Control? _frontUI;

    [Export]
    private PlayerOperationInterfaceInfo? _playerOperationInterfaceInfo;

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

        // 连接 GameLobby 的 BattleStarted 信号
        _gameLobby?.BattleStarted += OnBattleStarted;

        // 构造屏幕状态机（FrontUI 容器在战斗中整体隐藏）
        _screenMachine = new ScreenStateMachine(_frontUI, _gameLobby);

        // 默认显示主菜单
        _mainMenu?.OpenPanelFrom();

        GD.Print("[MainScene] _Ready Initialized.");
    }

    private void ValidateExports() {
        if (_mainMenu == null)
            GD.PrintErr("[MainScene] [Export] _mainMenu is not assigned!");
        if (_gameLobby == null)
            GD.PrintErr("[MainScene] [Export] _gameLobby is not assigned!");
        if (_frontUI == null)
            GD.PrintErr("[MainScene] [Export] _frontUI is not assigned!");
        if (_playerOperationInterfaceInfo == null)
            GD.PrintErr("[MainScene] [Export] _playerOperationInterfaceInfo is not assigned!");
        if (_unitManager == null)
            GD.PrintErr("[MainScene] [Export] _unitManager is not assigned!");
        if (_inputController == null)
            GD.PrintErr("[MainScene] [Export] _inputController is not assigned!");
    }

    // =============================================================
    // 进入/退出战斗
    // =============================================================

    private void OnBattleStarted(string roomId) {
        _roomId = roomId;
        _battleService = ServiceLocator.ClientService.RoomClient;

        GD.Print($"[MainScene] Battle started for room: {roomId}");

        // 生命周期事件（战斗阶段）由 MainScene 持有；单位事件归 BattleUnitManager
        if (_battleService != null) {
            _battleService.BattlePhaseChanged += OnBattlePhase;

            _unitManager?.Bind(_battleService, ServiceLocator.ClientService.RoomClient, roomId);
            _inputController?.Reset();
        }

        // 绑定 UI（通过 VM 触发 View 初始化）
        _playerOperationInterfaceInfo?.BindToBattle();

        // 隐藏整个前厅 UI（FrontUI + 全屏背景 Panel）
        _screenMachine?.EnterBattle();
        _inBattle = true;

        GD.Print("[MainScene] Entered battle.");
    }

    private void ExitBattle() {
        _battleService?.BattlePhaseChanged -= OnBattlePhase;

        // 子组件退订与清理
        _unitManager?.Unbind();
        _inputController?.Reset();

        // 解绑 UI
        _playerOperationInterfaceInfo?.UnbindFromBattle();

        _battleService = null;
        _roomId = "";
        _inBattle = false;

        // 恢复前厅 UI（FrontUI 容器 + 大厅面板）
        _screenMachine?.ExitBattle();

        EmitSignal(SignalName.BattleEnded);
        GD.Print("[MainScene] Exited battle.");
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
            GD.Print("[MainScene] Battle finished detected via LES sync.");
            CallDeferred(nameof(ExitBattle));
        }
    }

    /// <summary>
    /// 节点退出场景树：取消战斗服务事件订阅。
    /// </summary>
    public override void _ExitTree() {
        _battleService?.BattlePhaseChanged -= OnBattlePhase;
    }
}
