using Godot;

namespace DungeonChessBattle.ReplayUI;

/// <summary>
/// 回放控制条：播放/暂停、进度拖动与时间显示。
/// 通过导出引用绑定 ReplayCoordinator 与 UI 控件，按钮经 Godot 信号接线。
/// </summary>
public partial class ReplayHud : Control {
    /// <summary>回放编排器引用。</summary>
    [Export]
    private ReplayCoordinator? _coordinator;

    /// <summary>播放/暂停按钮。</summary>
    [Export]
    private Button? _playButton;

    /// <summary>进度滑条。</summary>
    [Export]
    private HSlider? _progressSlider;

    /// <summary>时间标签。</summary>
    [Export]
    private Label? _timeLabel;

    /// <summary>每帧刷新进度与时间文本；拖动中不覆盖滑条位置。</summary>
    public override void _Process(double delta) {
        var engine = _coordinator?.Engine;
        if (engine == null)
            return;

        if (_progressSlider is { } slider && !slider.HasFocus() && engine.TotalFrames > 0)
            slider.Value = engine.Frame / (double)engine.TotalFrames;

        if (_timeLabel != null) {
            double current = engine.Frame * engine.FixedDelta;
            double total = engine.TotalFrames * engine.FixedDelta;
            _timeLabel.Text = $"{FormatTime(current)} / {FormatTime(total)}";
        }

        if (_playButton != null)
            _playButton.Text = _coordinator?.IsPaused == true ? "播放" : "暂停";
    }

    /// <summary>播放/暂停按钮回调（Godot 信号接线）。</summary>
    public void OnPlayPressed() {
        _coordinator?.TogglePause();
    }

    /// <summary>进度拖动回调（Godot 信号接线）。</summary>
    public void OnSliderChanged(double value) {
        if (_coordinator is { } c && _progressSlider is { } slider && slider.HasFocus())
            c.SeekToFraction((float)value);
    }

    private static string FormatTime(double seconds) {
        int total = (int)seconds;
        return $"{total / 60:00}:{total % 60:00}";
    }
}
