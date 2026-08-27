using System;
using DungeonChessBattle.Replay.Shared;
using DungeonChessBattle.Replay;
using DungeonChessBattle.Game.GamePlayUI;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.MainScene;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.ReplayUI;

/// <summary>
/// 回放编排：加载回放字节流构建 <see cref="ReplayEngine"/>，按固定逻辑步长推进，
/// 复用 BattleInterface 的共享 <see cref="UnitShowManager"/> 对齐驱动单位展示。提供播放/暂停/倍速/拖动控制。
/// 由回放入口面板 LoadReplay 启动，退出时释放引擎与展示。
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

    /// <summary>单位展示管理器（复用 BattleInterface 共享实例，在线/回放同一所有者）。</summary>
    [Export]
    private UnitShowManager? _unitManager;

    /// <summary>回放控制条，启动/退出时切换显隐。</summary>
    [Export]
    private Control? _hud;

    /// <summary>状态变化信息渲染器（复用在线实例，受击/治疗/Buff 浮字）。</summary>
    [Export]
    private UnitStateChangeInfo? _stateChangeInfo;

    private ReplayEngine? _engine;
    private double _accumulator;
    private bool _isPaused;

    /// <summary>当前回放引擎，未加载时为 null。</summary>
    public ReplayEngine? Engine => _engine;

    /// <summary>回放是否已启动（引擎已加载且在播）。</summary>
    public bool IsActive => _engine != null;

    /// <summary>是否暂停。</summary>
    public bool IsPaused {
        get => _isPaused;
    }

    /// <summary>播放倍速。</summary>
    public float PlaySpeed {
        get; set;
    } = 1f;

    /// <summary>加载回放字节流并启动：解码、构建引擎、生成单位展示。</summary>
    public void LoadReplay(byte[] replayData) {
        ReplayRecordSnapshot snapshot;
        try {
            snapshot = ReplayRecordCoder.Decode(replayData);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "回放数据解码失败");
            return;
        }

        ReplayEngine engine;
        try {
            engine = new ReplayEngine(snapshot);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "回放引擎构建失败（内容版本不一致或配置缺失）");
            return;
        }

        _engine = engine;
        _accumulator = 0;
        _isPaused = false;
        _unitManager?.Bind(_engine);
        _stateChangeInfo?.Bind(_engine);
        ShowReplay();
        EmitSignal(SignalName.ReplayStarted);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("回放加载完成：{RoomId}，单位 {UnitCount}", snapshot.Header.RoomId, _engine.Units.Count);
    }

    /// <summary>每帧推进回放引擎：按倍速累积固定步长，未加载/暂停/结束时为空操作。</summary>
    public override void _Process(double delta) {
        var engine = _engine;
        if (engine == null || _isPaused || engine.IsFinished)
            return;

        _accumulator += delta * PlaySpeed;
        while (_accumulator >= engine.FixedDelta) {
            var events = engine.Step();
            _stateChangeInfo?.Consume(events);
            _accumulator -= engine.FixedDelta;
        }
        _unitManager?.Tick();
    }

    /// <summary>切换播放/暂停。</summary>
    public void TogglePause() => _isPaused = !_isPaused;

    /// <summary>按进度比例拖动（0~1），早于当前帧时引擎内部重置快进。</summary>
    public void SeekToFraction(float fraction) {
        if (_engine == null)
            return;
        _accumulator = 0;
        _engine.SeekTo((int)(fraction * _engine.TotalFrames));
    }

    /// <summary>退出回放：释放引擎与单位展示，恢复屏幕态。</summary>
    public void ExitReplay() {
        if (!IsActive)
            return;
        _unitManager?.Unbind();
        _stateChangeInfo?.Unbind();
        _engine = null;
        _accumulator = 0;
        _isPaused = false;
        HideReplay();
        EmitSignal(SignalName.ReplayFinished);
    }

    /// <summary>节点退出场景树：兜底释放引擎与展示并通知主场景恢复。</summary>
    public override void _ExitTree() {
        if (IsActive) {
            _unitManager?.Unbind();
            _stateChangeInfo?.Unbind();
            _engine = null;
            HideReplay();
            EmitSignal(SignalName.ReplayFinished);
        }
    }

    /// <summary>切换到回放表现：显示回放控制条（单位复用共享世界，由 UnitShowManager 生灭）。</summary>
    private void ShowReplay() {
        _hud?.Visible = true;
    }

    /// <summary>退出回放表现：隐藏回放控制条。</summary>
    private void HideReplay() {
        _hud?.Visible = false;
    }
}
