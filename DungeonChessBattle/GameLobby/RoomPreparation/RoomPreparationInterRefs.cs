using Godot;

namespace DungeonChessBattle;

/// <summary>
/// RoomPreparation 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class RoomPreparationInterRefs : Node {
    [Export]
    public Label? RoomNameLabel {
        get; set;
    }
    [Export]
    public Label? StatusLabel {
        get; set;
    }
    [Export]
    public GridContainer? UnitCardGrid {
        get; set;
    }
    [Export]
    public Button? CampAButton {
        get; set;
    }
    [Export]
    public Button? CampBButton {
        get; set;
    }
    [Export]
    public Button? StartBattleButton {
        get; set;
    }
    [Export]
    public Label? CampAListLabel {
        get; set;
    }
    [Export]
    public Label? CampBListLabel {
        get; set;
    }
    [Export]
    public PackedScene? UnitCardScene {
        get; set;
    }
}
