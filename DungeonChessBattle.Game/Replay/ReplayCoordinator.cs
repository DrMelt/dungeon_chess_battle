using System;
using DungeonChessBattle.Replay.Shared;
using DungeonChessBattle.Replay;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.Game.BattleScene;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.ReplayUI;

/// <summary>
/// 回放编排：`replay_assemble.tscn` 的根节点，以回放记录构建 <see cref="ReplayEngine"/>，按固定逻辑步长推进，
/// 把回放装配 <see cref="ReplayBattleViewSource"/> 注入共享 <see cref="BattleSessionContext"/> 作为表现层统一数据源，
/// 同场景的单位展示与浮字组件自持该数据源，与在线同口径驱动。提供播放/暂停/倍速/拖动控制。
/// 组装场景由 MainScene 在 StartReplay 时实例化、回放结束时释放；本节点管引擎生命周期与回放表现（ReplayUI）显隐。
/// 回放启动/结束经 <see cref="ReplayStartedEventHandler"/>、<see cref="ReplayFinishedEventHandler"/> 通知主场景切换屏幕态。
/// </summary>
public partial class ReplayCoordinator : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ReplayCoordinator> _logger =
        ServiceLocator.GetLogger<ReplayCoordinator>();

    /// <summary>回放已启动信号：字节流加载成功，通知主场景切换屏幕态。</summary>
    [Signal]
    public delegate void ReplayStartedEventHandler();

    /// <summary>回放已结束信号：通知主场景恢复前厅。</summary>
    [Signal]
    public delegate void ReplayFinishedEventHandler();

    /// <summary>回放表现容器（控制条与输入面板挂在它下面），启动/退出时整容器切显隐。</summary>
    [Export]
    private Control? _replayUI;

    /// <summary>战斗会话上下文（复用在线实例），回放期间作为表现层统一数据源。</summary>
    [Export]
    private BattleSessionContext? _sessionContext;

    private ReplayEngine? _engine;
    private double _accumulator;
    private bool _isPaused;

    /// <summary>当前回放引擎，未加载时为 null。</summary>
    public ReplayEngine? Engine => _engine;

    /// <summary>回放是否已启动（引擎已加载且在播）。</summary>
    public bool IsActive => _engine != null;

    /// <summary>是否暂停。</summary>
    public bool IsPaused => _isPaused;

    /// <summary>播放倍速。</summary>
    public float PlaySpeed {
        get; set;
    } = 1f;

    /// <summary>启动回放：以回放获取端解码并门控后的记录构建引擎，生成单位展示。</summary>
    public void LoadReplay(ReplayRecording recording) {
        ReplayEngine engine;
        try {
            engine = new ReplayEngine(recording);
        }
        catch (Exception ex) {
            // 引擎构造自带门控：配置缺失与版本不符都在这里挡下，不进入半启动状态
            _logger.LogError(ex, "回放引擎构建失败");
            return;
        }

        _engine = engine;
        _accumulator = 0;
        _isPaused = false;
        _sessionContext?.Bind(new ReplayBattleViewSource(engine));
        _replayUI?.Visible = true;
        EmitSignal(SignalName.ReplayStarted);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("回放加载完成：{RoomId}，单位 {UnitCount}", recording.Meta.RoomId, engine.Units.Count);
    }

    /// <summary>每帧推进回放引擎：按倍速累积固定步长，未加载/暂停/结束时为空操作。</summary>
    public override void _Process(double delta) {
        var engine = _engine;
        if (engine == null || _isPaused || engine.IsFinished)
            return;

        _accumulator += delta * PlaySpeed;
        while (_accumulator >= engine.FixedDelta) {
            var events = engine.Step();
            _sessionContext?.AppendEvents(events);
            _accumulator -= engine.FixedDelta;
        }
    }

    /// <summary>切换播放/暂停。</summary>
    public void TogglePause() => _isPaused = !_isPaused;

    /// <summary>按进度比例拖动（0~1）。</summary>
    public void SeekToFraction(float fraction) => SeekToFrame((int)(fraction * (_engine?.TotalFrames ?? 0)));

    /// <summary>跳转到指定相对帧，早于当前帧时引擎内部重置快进；播放/暂停态不变。</summary>
    public void SeekToFrame(int frame) {
        if (_engine == null)
            return;
        _accumulator = 0;
        _engine.SeekTo(Math.Clamp(frame, 0, _engine.TotalFrames));
    }

    /// <summary>退出回放：解绑统一数据源，释放引擎并恢复屏幕态。</summary>
    public void ExitReplay() {
        if (!IsActive)
            return;
        _sessionContext?.Unbind();
        _engine = null;
        _accumulator = 0;
        _isPaused = false;
        _replayUI?.Visible = false;
        EmitSignal(SignalName.ReplayFinished);
    }

    /// <summary>节点退出场景树：兜底释放引擎与展示并通知主场景恢复。</summary>
    public override void _ExitTree() => ExitReplay();
}
