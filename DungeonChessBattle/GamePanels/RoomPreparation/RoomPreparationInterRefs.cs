using Godot;

namespace DungeonChessBattle;

/// <summary>
/// RoomPreparation 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class RoomPreparationInterRefs : Node {
    /// <summary>房间名称副标题标签（房主 / 类别 / 人数）。</summary>
    [ExportGroup("Labels")]
    [Export]
    public Label? RoomNameLabel {
        get; private set;
    }
    /// <summary>操作状态提示标签。</summary>
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    /// <summary>已选单位文本列表标签。</summary>
    [Export]
    public Label? UnitListLabel {
        get; private set;
    }
    /// <summary>房间描述信息标签。</summary>
    [Export]
    public Label? InfoLabel {
        get; private set;
    }
    /// <summary>房间标题标签（大字标题）。</summary>
    [Export]
    public Label? TitleLabel {
        get; private set;
    }
    /// <summary>打开单位选择面板按钮。</summary>
    [ExportGroup("Buttons")]
    [Export]
    public Button? SelectUnitButton {
        get; private set;
    }
    /// <summary>开始战斗按钮。</summary>
    [Export]
    public Button? StartBattleButton {
        get; private set;
    }
    /// <summary>返回大厅按钮。</summary>
    [Export]
    public Button? BackButton {
        get; private set;
    }
    /// <summary>已选单位卡片网格容器。</summary>
    [ExportGroup("Containers")]
    [Export]
    public GridContainer? UnitCardGrid {
        get; private set;
    }
    /// <summary>单位选择面板引用。</summary>
    [ExportGroup("Panels")]
    [Export]
    public UnitSelectPanel? UnitSelectPanel {
        get; private set;
    }
    /// <summary>单个单位卡片使用的场景资源。</summary>
    [ExportGroup("Scene Resources")]
    [Export]
    public PackedScene? UnitCardScene {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (RoomNameLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] RoomNameLabel is not assigned!");
        if (StatusLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] StatusLabel is not assigned!");
        if (UnitListLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] UnitListLabel is not assigned!");
        if (InfoLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] InfoLabel is not assigned!");
        if (TitleLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] TitleLabel is not assigned!");
        if (SelectUnitButton == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] SelectUnitButton is not assigned!");
        if (StartBattleButton == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] StartBattleButton is not assigned!");
        if (BackButton == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] BackButton is not assigned!");
        if (UnitCardGrid == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] UnitCardGrid is not assigned!");
        if (UnitSelectPanel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] UnitSelectPanel is not assigned!");
        if (UnitCardScene == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] UnitCardScene is not assigned!");
    }
}
