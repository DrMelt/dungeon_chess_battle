using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// ModItem 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class ModItemInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ModItemInterRefs> _logger = ServiceLocator.GetLogger<ModItemInterRefs>();

    /// <summary>启用开关，只读展示启停状态。</summary>
    [Export]
    public CheckBox? EnableToggle {
        get; private set;
    }
    /// <summary>整行点击按钮，触发行上启停切换。</summary>
    [Export]
    public Button? RowButton {
        get; private set;
    }
    /// <summary>mod ID 列，tooltip 附目录与版本。</summary>
    [Export]
    public Label? IdLabel {
        get; private set;
    }
    /// <summary>构成列：数据代码与展示代码。</summary>
    [Export]
    public Label? CompositionLabel {
        get; private set;
    }
    /// <summary>依赖列，无依赖时隐藏。</summary>
    [Export]
    public Label? DependencyLabel {
        get; private set;
    }
    /// <summary>装载错误列，无错误时隐藏。</summary>
    [Export]
    public Label? ErrorLabel {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (EnableToggle == null)
            _logger.LogError("EnableToggle is not assigned!");
        if (RowButton == null)
            _logger.LogError("RowButton is not assigned!");
        if (IdLabel == null)
            _logger.LogError("IdLabel is not assigned!");
        if (CompositionLabel == null)
            _logger.LogError("CompositionLabel is not assigned!");
        if (DependencyLabel == null)
            _logger.LogError("DependencyLabel is not assigned!");
        if (ErrorLabel == null)
            _logger.LogError("ErrorLabel is not assigned!");
    }
}
