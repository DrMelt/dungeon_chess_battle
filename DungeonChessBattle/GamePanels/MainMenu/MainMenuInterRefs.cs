using Godot;

namespace DungeonChessBattle;

/// <summary>
/// MainMenu 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class MainMenuInterRefs : Node {
    [Export]
    public LineEdit? HostInput {
        get; private set;
    }
    [Export]
    public LineEdit? PortInput {
        get; private set;
    }
    [Export]
    public Button? ConnectButton {
        get; private set;
    }
    [Export]
    public Button? ServerManageButton {
        get; private set;
    }
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    [Export]
    public LineEdit? UserNameInput {
        get; private set;
    }
    [Export]
    public LineEdit? PasswordInput {
        get; private set;
    }

    public override void _Ready() {
        if (HostInput == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] HostInput is not assigned!");
        if (PortInput == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] PortInput is not assigned!");
        if (ConnectButton == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] ConnectButton is not assigned!");
        if (ServerManageButton == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] ServerManageButton is not assigned!");
        if (StatusLabel == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] StatusLabel is not assigned!");
        if (UserNameInput == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] UserNameInput is not assigned!");
        if (PasswordInput == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] PasswordInput is not assigned!");
    }
}
