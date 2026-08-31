using System;
using System.Collections.Generic;
using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.GamePlayUI;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;
using BattlePhase = DungeonChessBattle.Battle.Shared.Combat.BattlePhase;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 战斗编排器：战斗子系统（UnitShowManager/BattleSessionContext/BattleInputController/DungeonEnv）
/// 的生命周期与阶段分发中枢。
/// 进入/退出战斗时统一 Bind/Unbind 各子组件并订阅战斗阶段事件；Running 阶段与应用副本环境主题，
/// Finished 阶段经 OnBattleFinished 回调交还 MainScene 走应用级退出。
/// 数据查询仍经 BattleSessionContext 门面，本组件不做数据投影。
/// 由 MainScene 在战斗启动时 EnterBattle、退出时 ExitBattle，帧循环经 Tick 推进。
/// </summary>
public partial class BattleCoordinator : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleCoordinator> _logger = ServiceLocator.GetLogger<BattleCoordinator>();

    /// <summary>单位展示管理器引用。</summary>
    [Export]
    private UnitShowManager? _unitManager;

    /// <summary>战斗会话上下文引用。</summary>
    [Export]
    private BattleSessionContext? _sessionContext;

    /// <summary>战斗输入控制器引用。</summary>
    [Export]
    private BattleInputController? _inputController;

    /// <summary>地牢环境根节点引用。</summary>
    [Export]
    private DungeonEnv? _dungeonEnv;

    /// <summary>状态变化信息渲染器引用（受击/治疗/Buff 浮字），喂入战斗事件。</summary>
    [Export]
    private UnitStateChangeInfo? _stateChangeInfo;

    /// <summary>当前战斗服务（EnterBattle 时注入，用于阶段订阅与输入提交）。</summary>
    private IClientBattleService? _battleService;

    /// <summary>当前房间 ID（EnterBattle 时注入，用于阶段事件过滤）。</summary>
    private string _roomId = "";

    /// <summary>是否已在战斗中（子组件已绑定）。</summary>
    public bool IsInBattle {
        get; private set;
    }

    /// <summary>战斗完成回调（Finished 阶段触发），由 MainScene 注入应用级退出流程。</summary>
    public Action? OnBattleFinished;

    /// <summary>
    /// 进入战斗：重连时先退出旧绑定，再订阅阶段事件、绑定全部子组件并应用副本环境主题。
    /// </summary>
    public void EnterBattle(string roomId) {
        if (IsInBattle)
            ExitBattle();

        _roomId = roomId;
        var roomClient = ServiceLocator.ClientService.RoomClient;
        _battleService = roomClient;

        roomClient.BattlePhaseChanged += OnBattlePhase;
        _unitManager?.Bind(roomClient);
        _stateChangeInfo?.Bind(roomClient);
        _battleService.BattleEventsReceived += OnBattleEvents;
        _sessionContext?.Bind(roomClient, _roomId);
        _inputController?.Reset();

        // 按房间选中副本应用环境主题（地面/天空/光照差异化）
        _dungeonEnv?.ApplyDungeonTheme(_sessionContext?.DungeonKey);
        IsInBattle = true;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Battle entered: {RoomId}", roomId);
    }

    /// <summary>退出战斗：退订阶段事件、解绑全部子组件并恢复默认环境主题。</summary>
    public void ExitBattle() {
        if (!IsInBattle)
            return;

        _battleService?.BattlePhaseChanged -= OnBattlePhase;
        _battleService?.BattleEventsReceived -= OnBattleEvents;
        _unitManager?.Unbind();
        _stateChangeInfo?.Unbind();
        _sessionContext?.Unbind();
        _inputController?.Reset();
        _dungeonEnv?.ResetTheme();

        _battleService = null;
        _roomId = "";
        IsInBattle = false;
    }

    /// <summary>每帧推进战斗输入采集，未在战斗中为空操作；单位视图对齐由 UnitShowManager 自身帧循环负责。</summary>
    public override void _Process(double delta) {
        if (!IsInBattle || _battleService == null)
            return;
        _inputController?.Tick(_battleService);
    }

    /// <summary>节点退出场景树：兜底退出战斗（防止中途释放导致事件悬挂）。</summary>
    public override void _ExitTree() {
        ExitBattle();
    }

    /// <summary>战斗事件流订阅：转发给状态变化渲染器弹出现时浮字。</summary>
    private void OnBattleEvents(string roomId, IReadOnlyList<IBattleEvent> events) {
        if (roomId != _roomId)
            return;
        _stateChangeInfo?.Consume(events);
    }

    private void OnBattlePhase(string roomId, BattlePhase phase) {
        if (roomId != _roomId)
            return;
        CallDeferred(nameof(DeferredBattlePhase), (int)phase);
    }

    private void DeferredBattlePhase(int phase) {
        if (phase == (int)BattlePhase.Running) {
            // 战斗真正开始时房间实体已同步，DungeonKey 可用；
            // 阵营关系未装配属时序故障，经会话侧响应校验后再应用副本环境主题
            _sessionContext?.OnBattleRunning();
            _dungeonEnv?.ApplyDungeonTheme(_sessionContext?.DungeonKey);
        }

        if (phase == (int)BattlePhase.Finished) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Battle finished detected via LES sync.");
            OnBattleFinished?.Invoke();
        }
    }
}
