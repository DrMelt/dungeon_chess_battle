using Godot;

namespace DungeonChessBattle;

/// <summary>
/// RoomPreparation 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class RoomPreparationInterRefs : Node {
    [Export]
    public Label? RoomNameLabel {
        get; private set;
    }
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    [Export]
    public GridContainer? UnitCardGrid {
        get; private set;
    }
    [Export]
    public Button? CampAButton {
        get; private set;
    }
    [Export]
    public Button? CampBButton {
        get; private set;
    }
    [Export]
    public Button? StartBattleButton {
        get; private set;
    }
    [Export]
    public Label? CampAListLabel {
        get; private set;
    }
    [Export]
    public Label? CampBListLabel {
        get; private set;
    }
    [Export]
    public PackedScene? UnitCardScene {
        get; private set;
    }
    [Export]
    public Button? BackButton {
        get; private set;
    }
    [Export]
    public Label? InfoLabel {
        get; private set;
    }
    [Export]
    public Label? TitleLabel {
        get; private set;
    }

    public override void _Ready() {
        if (RoomNameLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] RoomNameLabel is not assigned!");
        if (StatusLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] StatusLabel is not assigned!");
        if (UnitCardGrid == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] UnitCardGrid is not assigned!");
        if (CampAButton == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] CampAButton is not assigned!");
        if (CampBButton == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] CampBButton is not assigned!");
        if (StartBattleButton == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] StartBattleButton is not assigned!");
        if (CampAListLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] CampAListLabel is not assigned!");
        if (CampBListLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] CampBListLabel is not assigned!");
        if (UnitCardScene == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] UnitCardScene is not assigned!");
        if (BackButton == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] BackButton is not assigned!");
        if (InfoLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] InfoLabel is not assigned!");
        if (TitleLabel == null)
            GD.PrintErr("[RoomPreparationInterRefs] [Export] TitleLabel is not assigned!");
    }
}
