using System;
using System.Collections.Generic;
using DungeonChessBattle.Battle.Client;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;
using BattlePhase = DungeonChessBattle.Battle.Shared.Combat.BattlePhase;

namespace DungeonChessBattle.Game.BattleScene;

/// <summary>
/// 战斗编排器：`battle_assemble.tscn` 的根节点，战斗子系统（BattleSessionContext/BattleInputController/DungeonEnv）
/// 的生命周期与阶段分发中枢。
/// 进入战斗时单独构建在线装配（视图源 + 命令）注入统一数据源并订阅战斗阶段事件，退出时解绑；
/// Running 阶段收敛副本环境（实体同步后键变化时整棵重建），Finished 阶段经 OnBattleFinished 回调交还 MainScene 走应用级退出。
/// 展示组件自持统一数据源取数与自取帧事件，本组件不碰传输对象、不逐组件接线。
/// 组装场景由 MainScene 进战斗时实例化、退出时释放，帧循环经 _Process 推进。
/// </summary>
public partial class BattleCoordinator : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleCoordinator> _logger = ServiceLocator.GetLogger<BattleCoordinator>();

    /// <summary>战斗会话上下文引用。</summary>
    [Export]
    private BattleSessionContext? _sessionContext;

    /// <summary>战斗输入控制器引用。</summary>
    [Export]
    private BattleInputController? _inputController;

    /// <summary>当前战斗环境实例，EnterBattle 按会话副本键经资源表创建，ExitBattle 销毁。</summary>
    private DungeonEnv? _dungeonEnv;

    /// <summary>环境实例对应的副本键；会话键变化时重建，覆盖实体同步前后的时序差异。</summary>
    private string? _dungeonEnvKey;

    /// <summary>当前战斗服务（EnterBattle 时注入，用于阶段订阅与输入提交）。</summary>
    private IClientBattleService? _battleService;

    /// <summary>当前房间 ID（EnterBattle 时注入，用于阶段事件过滤）。</summary>
    private string _roomId = "";

    /// <summary>是否已在战斗中（统一数据源与阶段事件已绑定）。</summary>
    public bool IsInBattle {
        get; private set;
    }

    /// <summary>战斗完成回调（Finished 阶段触发），由 MainScene 注入应用级退出流程。</summary>
    public Action? OnBattleFinished;

    /// <summary>
    /// 确保战斗环境实例与会话副本键一致：键为 null（实体未同步）或键变化的首次装配，
    /// 场景模板内已固化主题，键变化只能整棵重建；未同步键回退默认副本场景由资源工厂裁决。
    /// </summary>
    private void EnsureEnvironment() {
        string? key = _sessionContext?.DungeonKey;
        if (_dungeonEnv != null && _dungeonEnvKey == key)
            return;
        if (_dungeonEnv != null) {
            RemoveChild(_dungeonEnv);
            _dungeonEnv.QueueFree();
            _dungeonEnv = null;
            _dungeonEnvKey = null;
        }
        var env = ResourceTables.Dungeons.InstantiateEnvironment(key);
        if (env == null)
            return;
        AddChild(env);
        _dungeonEnv = env;
        _dungeonEnvKey = key;
    }

    /// <summary>进入战斗：重连时先退出旧绑定，再订阅阶段事件、绑定统一数据源并装配副本环境。</summary>
    public void EnterBattle(string roomId) {
        if (IsInBattle)
            ExitBattle();

        _roomId = roomId;
        var session = ServiceLocator.ClientService.RoomSession;
        _battleService = session;

        session.BattlePhaseChanged += OnBattlePhase;
        _battleService.BattleEventsReceived += OnBattleEvents;
        _sessionContext?.Bind(new OnlineBattleViewSource(session), new BattleSessionCommand(session, _roomId));
        _inputController?.Reset();

        // 按房间选中副本创建环境（场景模板内固化主题，经副本资源表）
        EnsureEnvironment();
        IsInBattle = true;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Battle entered: {RoomId}", roomId);
    }

    /// <summary>退出战斗：退订阶段事件、解绑统一数据源并销毁战斗环境实例。</summary>
    public void ExitBattle() {
        if (!IsInBattle)
            return;

        _battleService?.BattlePhaseChanged -= OnBattlePhase;
        _battleService?.BattleEventsReceived -= OnBattleEvents;
        _sessionContext?.Unbind();
        _inputController?.Reset();

        // 销毁战斗环境实例
        if (_dungeonEnv != null) {
            RemoveChild(_dungeonEnv);
            _dungeonEnv.QueueFree();
        }
        _dungeonEnv = null;
        _dungeonEnvKey = null;

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

    /// <summary>战斗事件流订阅：交统一数据源入帧缓冲与事件日志。</summary>
    private void OnBattleEvents(string roomId, IReadOnlyList<IBattleEvent> events) {
        if (roomId != _roomId)
            return;
        _sessionContext?.AppendEvents(events);
    }

    private void OnBattlePhase(string roomId, BattlePhase phase) {
        if (roomId != _roomId)
            return;
        CallDeferred(nameof(DeferredBattlePhase), (int)phase);
    }

    private void DeferredBattlePhase(int phase) {
        if (phase == (int)BattlePhase.Running) {
            // 战斗真正开始时房间实体已同步，DungeonKey 可用；
            // 阵营关系未装配属时序故障，经会话侧响应校验后再收敛环境（键变化时整棵重建）
            _sessionContext?.OnBattleRunning();
            EnsureEnvironment();
        }

        if (phase == (int)BattlePhase.Finished) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Battle finished detected via LES sync.");
            OnBattleFinished?.Invoke();
        }
    }
}
