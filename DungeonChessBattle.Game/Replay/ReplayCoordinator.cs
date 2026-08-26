using System;
using DungeonChessBattle.Replay.Shared;
using DungeonChessBattle.Replay;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.MainScene;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.ReplayUI;

/// <summary>
/// 回放场景编排：加载回放字节流构建 <see cref="ReplayEngine"/>，按固定逻辑步长推进，
/// 经 UnitShowManager 对齐驱动单位展示。提供播放/暂停/倍速/拖动控制。
/// 由回放入口面板 LoadReplay 启动，退出时释放引擎与展示。
/// </summary>
public partial class ReplayCoordinator : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ReplayCoordinator> _logger =
        ServiceLocator.GetLogger<ReplayCoordinator>();

    /// <summary>单位展示管理器（回放数据源对齐驱动）。</summary>
    [Export]
    private UnitShowManager? _unitManager;

    private ReplayEngine? _engine;
    private double _accumulator;
    private bool _isPaused;

    /// <summary>当前回放引擎，未加载时为 null。</summary>
    public ReplayEngine? Engine => _engine;

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
            engine.Step();
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

    /// <summary>退出回放：释放引擎与单位展示。</summary>
    public void ExitReplay() {
        _unitManager?.Unbind();
        _engine = null;
        _accumulator = 0;
        _isPaused = false;
    }

    /// <summary>节点退出场景树：兜底释放引擎与展示。</summary>
    public override void _ExitTree() {
        _unitManager?.Unbind();
        _engine = null;
    }
}
