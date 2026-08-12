using Godot;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// UnitSelectPanel 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class UnitSelectPanelInterRefs : Node {
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
            GD.PrintErr("[UnitSelectPanelInterRefs] [Export] TitleLabel is not assigned!");
        if (UnitCardGrid == null)
            GD.PrintErr("[UnitSelectPanelInterRefs] [Export] UnitCardGrid is not assigned!");
        if (BackButton == null)
            GD.PrintErr("[UnitSelectPanelInterRefs] [Export] BackButton is not assigned!");
        if (UnitCardScene == null)
            GD.PrintErr("[UnitSelectPanelInterRefs] [Export] UnitCardScene is not assigned!");
    }
}
