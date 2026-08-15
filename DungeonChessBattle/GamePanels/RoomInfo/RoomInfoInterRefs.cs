using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// RoomInfo 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class RoomInfoInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<RoomInfoInterRefs> _logger = ServiceLocator.GetLogger<RoomInfoInterRefs>();

    /// <summary>副本名标签。</summary>
    [Export]
    public Label? DungeonLabel {
        get; private set;
    }
    /// <summary>密码标记标签。</summary>
    [Export]
    public Label? PasswordLabel {
        get; private set;
    }
    /// <summary>房间状态标签。</summary>
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    /// <summary>房主标签。</summary>
    [Export]
    public Label? HostLabel {
        get; private set;
    }
    /// <summary>人数标签。</summary>
    [Export]
    public Label? PlayersLabel {
        get; private set;
    }
    /// <summary>副本介绍标签。</summary>
    [Export]
    public Label? DescriptionLabel {
        get; private set;
    }
    /// <summary>背景面板，用于高亮效果。</summary>
    [Export]
    public Panel? BgPanel {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (DungeonLabel == null)
            _logger.LogError("DungeonLabel is not assigned!");
        if (PasswordLabel == null)
            _logger.LogError("PasswordLabel is not assigned!");
        if (StatusLabel == null)
            _logger.LogError("StatusLabel is not assigned!");
        if (HostLabel == null)
            _logger.LogError("HostLabel is not assigned!");
        if (PlayersLabel == null)
            _logger.LogError("PlayersLabel is not assigned!");
        if (DescriptionLabel == null)
            _logger.LogError("DescriptionLabel is not assigned!");
        if (BgPanel == null)
            _logger.LogError("BgPanel is not assigned!");
    }
}
