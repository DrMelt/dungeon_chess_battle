using Godot;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// UnitCard 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class UnitCardInterRefs : Node {
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
            GD.PrintErr("[UnitCardInterRefs] [Export] NameLabel is not assigned!");
        if (UserNameLabel == null)
            GD.PrintErr("[UnitCardInterRefs] [Export] UserNameLabel is not assigned!");
        if (HpLabel == null)
            GD.PrintErr("[UnitCardInterRefs] [Export] HpLabel is not assigned!");
        if (HpValueLabel == null)
            GD.PrintErr("[UnitCardInterRefs] [Export] HpValueLabel is not assigned!");
        if (BgPanel == null)
            GD.PrintErr("[UnitCardInterRefs] [Export] BgPanel is not assigned!");
    }
}
