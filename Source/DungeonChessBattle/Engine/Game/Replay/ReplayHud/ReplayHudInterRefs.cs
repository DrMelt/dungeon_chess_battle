using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.ReplayUI;

/// <summary>
/// ReplayHud 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class ReplayHudInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ReplayHudInterRefs> _logger = ServiceLocator.GetLogger<ReplayHudInterRefs>();

    /// <summary>播放/暂停按钮。</summary>
    [Export]
    public Button? PlayButton {
        get; private set;
    }
    /// <summary>倍速循环按钮。</summary>
    [Export]
    public Button? SpeedButton {
        get; private set;
    }
    /// <summary>退出回放按钮。</summary>
    [Export]
    public Button? ExitButton {
        get; private set;
    }
    /// <summary>进度滑条。</summary>
    [Export]
    public HSlider? ProgressSlider {
        get; private set;
    }
    /// <summary>时间标签。</summary>
    [Export]
    public Label? TimeLabel {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (PlayButton == null)
            _logger.LogError("PlayButton is not assigned!");
        if (SpeedButton == null)
            _logger.LogError("SpeedButton is not assigned!");
        if (ExitButton == null)
            _logger.LogError("ExitButton is not assigned!");
        if (ProgressSlider == null)
            _logger.LogError("ProgressSlider is not assigned!");
        if (TimeLabel == null)
            _logger.LogError("TimeLabel is not assigned!");
    }
}
