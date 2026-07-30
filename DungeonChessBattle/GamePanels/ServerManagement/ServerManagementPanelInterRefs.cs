using Godot;

namespace DungeonChessBattle;

/// <summary>
/// ServerManagementPanel 的导出引用集合。
/// </summary>
public partial class ServerManagementPanelInterRefs : Node {
    [Export]
    public LineEdit? PortInput {
        get; private set;
    }
    [Export]
    public Button? StartButton {
        get; private set;
    }
    [Export]
    public Button? StopButton {
        get; private set;
    }
    [Export]
    public Button? CloseButton {
        get; private set;
    }
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    [Export]
    public Label? LogLabel {
        get; private set;
    }

    public override void _Ready() {
        if (PortInput == null)
            GD.PrintErr("[ServerManagementPanelInterRefs] [Export] PortInput is not assigned!");
        if (StartButton == null)
            GD.PrintErr("[ServerManagementPanelInterRefs] [Export] StartButton is not assigned!");
        if (StopButton == null)
            GD.PrintErr("[ServerManagementPanelInterRefs] [Export] StopButton is not assigned!");
        if (CloseButton == null)
            GD.PrintErr("[ServerManagementPanelInterRefs] [Export] CloseButton is not assigned!");
        if (StatusLabel == null)
            GD.PrintErr("[ServerManagementPanelInterRefs] [Export] StatusLabel is not assigned!");
        if (LogLabel == null)
            GD.PrintErr("[ServerManagementPanelInterRefs] [Export] LogLabel is not assigned!");
    }
}
