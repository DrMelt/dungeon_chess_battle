using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// UnitCard 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class UnitCardInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitCardInterRefs> _logger = ServiceLocator.GetLogger<UnitCardInterRefs>();

    /// <summary>单位名称标签。</summary>
    [Export]
    public Label? NameLabel {
        get; private set;
    }
    /// <summary>用户名标签。</summary>
    [Export]
    public Label? UserNameLabel {
        get; private set;
    }
    /// <summary>血量标题标签。</summary>
    [Export]
    public Label? HpLabel {
        get; private set;
    }
    /// <summary>血量数值标签。</summary>
    [Export]
    public Label? HpValueLabel {
        get; private set;
    }
    /// <summary>卡片背景面板，用于高亮效果。</summary>
    [Export]
    public Panel? BgPanel {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (NameLabel == null)
            _logger.LogError("NameLabel is not assigned!");
        if (UserNameLabel == null)
            _logger.LogError("UserNameLabel is not assigned!");
        if (HpLabel == null)
            _logger.LogError("HpLabel is not assigned!");
        if (HpValueLabel == null)
            _logger.LogError("HpValueLabel is not assigned!");
        if (BgPanel == null)
            _logger.LogError("BgPanel is not assigned!");
    }
}
