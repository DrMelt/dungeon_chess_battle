using Godot;

namespace DungeonChessBattle;

/// <summary>
/// BattlePanel 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class BattlePanelInterRefs : Node {
    [Export]
    public UnitsInScene_Show? UnitsShow {
        get; private set;
    }

    [Export]
    public Camera3D? BattleCamera {
        get; private set;
    }

    [Export]
    public Node2d_UserUI? UserUI {
        get; private set;
    }

    [Export]
    public UserOperationInterfaceInfo? UserOpInfo {
        get; private set;
    }

    [Export]
    public PackedScene? UnitShowScene {
        get; private set;
    }

    [Export]
    public CampStartPoints? CampAStart {
        get; private set;
    }

    [Export]
    public CampStartPoints? CampBStart {
        get; private set;
    }

    [Export]
    public Button? BackButton {
        get; private set;
    }

    [Export]
    public Label? StatusLabel {
        get; private set;
    }

    public override void _Ready() {
        ValidateExports();
    }

    private void ValidateExports() {
        if (UnitsShow == null)
            GD.PrintErr("[BattlePanelInterRefs] [Export] UnitsShow is not assigned!");
        if (BattleCamera == null)
            GD.PrintErr("[BattlePanelInterRefs] [Export] BattleCamera is not assigned!");
        if (UserUI == null)
            GD.PrintErr("[BattlePanelInterRefs] [Export] UserUI is not assigned!");
        if (UserOpInfo == null)
            GD.PrintErr("[BattlePanelInterRefs] [Export] UserOpInfo is not assigned!");
        if (UnitShowScene == null)
            GD.PrintErr("[BattlePanelInterRefs] [Export] UnitShowScene is not assigned!");
        if (CampAStart == null)
            GD.PrintErr("[BattlePanelInterRefs] [Export] CampAStart is not assigned!");
        if (CampBStart == null)
            GD.PrintErr("[BattlePanelInterRefs] [Export] CampBStart is not assigned!");
        if (BackButton == null)
            GD.PrintErr("[BattlePanelInterRefs] [Export] BackButton is not assigned!");
    }
}
