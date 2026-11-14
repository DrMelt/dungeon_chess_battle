using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// UnitSelectPanel 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class UnitSelectPanelInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitSelectPanelInterRefs> _logger = ServiceLocator.GetLogger<UnitSelectPanelInterRefs>();

    /// <summary>面板标题标签。</summary>
    [Export]
    public Label? TitleLabel {
        get; private set;
    }
    /// <summary>单位卡片网格容器。</summary>
    [Export]
    public GridContainer? UnitCardGrid {
        get; private set;
    }
    /// <summary>返回按钮。</summary>
    [Export]
    public Button? BackButton {
        get; private set;
    }
    /// <summary>单个单位卡片使用的场景资源。</summary>
    [Export]
    public PackedScene? UnitCardScene {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (TitleLabel == null)
            _logger.LogError("TitleLabel is not assigned!");
        if (UnitCardGrid == null)
            _logger.LogError("UnitCardGrid is not assigned!");
        if (BackButton == null)
            _logger.LogError("BackButton is not assigned!");
        if (UnitCardScene == null)
            _logger.LogError("UnitCardScene is not assigned!");
    }
}
