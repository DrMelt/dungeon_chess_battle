using System;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.ReplayUI;

/// <summary>
/// 回放控制条：播放/暂停、倍速、进度拖动与时间显示。
/// 回放编排器是跨场景依赖，由 MainScene 注入；UI 控件经 ReplayHudInterRefs 绑定并在 _Ready 接线。
/// </summary>
public partial class ReplayHud : Control {
    private static readonly ILogger<ReplayHud> _logger = ServiceLocator.GetLogger<ReplayHud>();

    /// <summary>回放编排器引用，跨场景依赖，由 MainScene 注入。</summary>
    [Export]
    private ReplayCoordinator? _coordinator;

    /// <summary>导出引用集合节点。</summary>
    private ReplayHudInterRefs? _refs;

    /// <summary>可选播放倍速档位，循环切换。</summary>
    private static readonly float[] Speeds = [1f, 2f, 4f];

    /// <summary>暂停态按钮文案。</summary>
    private const string PauseText = "暂停";
    /// <summary>播放态按钮文案。</summary>
    private const string PlayText = "播放";
    /// <summary>倍速按钮文案模板。</summary>
    private const string SpeedText = "倍速 {0:0}x";

    /// <summary>节点就绪：获取引用集合并绑定控制按钮与进度滑条。</summary>
    public override void _Ready() {
        _refs = GetNode<ReplayHudInterRefs>("ReplayHudInterRefs");
        if (_refs is null) {
            _logger.LogError("ReplayHudInterRefs node not found.");
            return;
        }

        _refs.PlayButton?.Pressed += OnPlayPressed;
        _refs.SpeedButton?.Pressed += OnSpeedPressed;
        _refs.ExitButton?.Pressed += OnExitPressed;
        _refs.ProgressSlider?.ValueChanged += OnSliderChanged;
    }

    /// <summary>每帧刷新进度与时间文本；拖动中不覆盖滑条位置。</summary>
    public override void _Process(double delta) {
        var engine = _coordinator?.Engine;
        if (engine == null || _refs == null)
            return;

        if (_refs.ProgressSlider is { } slider && !slider.HasFocus() && engine.TotalFrames > 0)
            slider.Value = engine.Frame / (double)engine.TotalFrames;

        if (_refs.TimeLabel is { } timeLabel) {
            double current = engine.Frame * engine.FixedDelta;
            double total = engine.TotalFrames * engine.FixedDelta;
            timeLabel.Text = $"{FormatTime(current)} / {FormatTime(total)}";
        }

        if (_refs.PlayButton is { } play)
            play.Text = _coordinator?.IsPaused == true ? PlayText : PauseText;
        if (_refs.SpeedButton is { } speed)
            speed.Text = string.Format(SpeedText, _coordinator?.PlaySpeed ?? 1f);
    }

    /// <summary>播放/暂停按钮回调。</summary>
    private void OnPlayPressed() {
        _coordinator?.TogglePause();
    }

    /// <summary>退出回放按钮回调。</summary>
    private void OnExitPressed() {
        _coordinator?.ExitReplay();
    }

    /// <summary>倍速按钮回调：在当前档位基础上循环切换。</summary>
    private void OnSpeedPressed() {
        if (_coordinator == null)
            return;
        int index = Array.IndexOf(Speeds, _coordinator.PlaySpeed);
        _coordinator.PlaySpeed = Speeds[(index + 1) % Speeds.Length];
    }

    /// <summary>进度拖动回调：仅滑条持焦即正在拖动时跳转到对应比例。</summary>
    private void OnSliderChanged(double value) {
        if (_coordinator is { } c && _refs?.ProgressSlider is { } slider && slider.HasFocus())
            c.SeekToFraction((float)value);
    }

    private static string FormatTime(double seconds) {
        int total = (int)seconds;
        return $"{total / 60:00}:{total % 60:00}";
    }
}
