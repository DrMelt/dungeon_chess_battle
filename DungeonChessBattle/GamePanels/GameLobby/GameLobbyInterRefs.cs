using Godot;

namespace DungeonChessBattle;

/// <summary>
/// GameLobby 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class GameLobbyInterRefs : Node {
    [Export]
    public LineEdit? RoomNameInput {
        get; private set;
    }
    [Export]
    public Button? CreateButton {
        get; private set;
    }
    [Export]
    public Button? RefreshButton {
        get; private set;
    }
    [Export]
    public Button? JoinButton {
        get; private set;
    }
    [Export]
    public Label? DetailLabel {
        get; private set;
    }
    [Export]
    public BoxContainer? RoomListContainer {
        get; private set;
    }
    [Export]
    public PackedScene? RoomInfoScene {
        get; private set;
    }
    [Export]
    public Button? BackButton {
        get; private set;
    }

    public override void _Ready() {
        if (RoomNameInput == null)
            GD.PrintErr("[GameLobbyInterRefs] [Export] RoomNameInput is not assigned!");
        if (CreateButton == null)
            GD.PrintErr("[GameLobbyInterRefs] [Export] CreateButton is not assigned!");
        if (RefreshButton == null)
            GD.PrintErr("[GameLobbyInterRefs] [Export] RefreshButton is not assigned!");
        if (JoinButton == null)
            GD.PrintErr("[GameLobbyInterRefs] [Export] JoinButton is not assigned!");
        if (DetailLabel == null)
            GD.PrintErr("[GameLobbyInterRefs] [Export] DetailLabel is not assigned!");
        if (RoomListContainer == null)
            GD.PrintErr("[GameLobbyInterRefs] [Export] RoomListContainer is not assigned!");
        if (RoomInfoScene == null)
            GD.PrintErr("[GameLobbyInterRefs] [Export] RoomInfoScene is not assigned!");
        if (BackButton == null)
            GD.PrintErr("[GameLobbyInterRefs] [Export] BackButton is not assigned!");
    }
}