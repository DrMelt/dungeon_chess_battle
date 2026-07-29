using Godot;

namespace DungeonChessBattle;

/// <summary>
/// MainMenu 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class MainMenuInterRefs : Node {
    [Export]
    public LineEdit? HostInput {
        get; set;
    }
    [Export]
    public LineEdit? PortInput {
        get; set;
    }
    [Export]
    public Button? ConnectButton {
        get; set;
    }
    [Export]
    public Button? ServerManageButton {
        get; set;
    }
    [Export]
    public Label? StatusLabel {
        get; set;
    }
}
