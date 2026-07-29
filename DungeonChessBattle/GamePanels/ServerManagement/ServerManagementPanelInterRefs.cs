using Godot;

namespace DungeonChessBattle;

/// <summary>
/// ServerManagementPanel 的导出引用集合。
/// </summary>
public partial class ServerManagementPanelInterRefs : Node {
    [Export]
    public LineEdit? PortInput { get; set; }

    [Export]
    public Button? StartButton { get; set; }

    [Export]
    public Button? StopButton { get; set; }

    [Export]
    public Button? CloseButton { get; set; }

    [Export]
    public Label? StatusLabel { get; set; }

    [Export]
    public Label? LogLabel { get; set; }
}