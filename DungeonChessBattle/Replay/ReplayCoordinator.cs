using System;
using DungeonChessBattle.Client.Replay;
using DungeonChessBattle.Protocol.Replay;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Replay;

/// <summary>
/// 回放场景编排：加载回放字节流构建 <see cref="ReplayEngine"/>，按固定逻辑步长推进，
/// 生成单位展示节点并驱动。提供播放/暂停/倍速/拖动控制。
/// 由回放入口面板 LoadReplay 启动，退出时释放引擎与展示。
/// </summary>
public partial class ReplayCoordinator : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ReplayCoordinator> _logger =
        Services.ServiceLocator.GetLogger<ReplayCoordinator>();

    /// <summary>单位展示场景（含 ReplayUnitShow 脚本）。</summary>
    [Export]
    private PackedScene? _unitShowScene;

    private ReplayEngine? _engine;
    private readonly System.Collections.Generic.Dictionary<ushort, ReplayUnitShow> _shows = [];
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

        _engine = new ReplayEngine(snapshot);
        _accumulator = 0;
        _isPaused = false;
        SpawnUnitShows();
        _logger.LogInformation("回放加载完成：{RoomId}，单位 {UnitCount}", snapshot.Header.RoomId, _shows.Count);
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
        foreach (var (_, show) in _shows)
            show.QueueFree();
        _shows.Clear();
        _engine = null;
        _accumulator = 0;
        _isPaused = false;
    }

    /// <summary>按引擎单位视图生成展示节点，加载与重置时调用。</summary>
    private void SpawnUnitShows() {
        foreach (var (_, show) in _shows)
            show.QueueFree();
        _shows.Clear();

        var engine = _engine;
        if (engine == null || _unitShowScene == null)
            return;

        foreach (var (netId, view) in engine.UnitViews) {
            var show = _unitShowScene.Instantiate<ReplayUnitShow>();
            show.View = view;
            AddChild(show);
            _shows[netId] = show;
        }
    }

    /// <summary>节点退出场景树：兜底释放引擎。</summary>
    public override void _ExitTree() {
        _engine = null;
        _shows.Clear();
    }
}
