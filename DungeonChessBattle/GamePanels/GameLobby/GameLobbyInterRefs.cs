using Godot;

namespace DungeonChessBattle;

/// <summary>
/// GameLobby 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class GameLobbyInterRefs : Node {
    [Export] public LineEdit? RoomNameInput { get; set; }
    [Export] public Button? CreateButton { get; set; }
    [Export] public Button? RefreshButton { get; set; }
    [Export] public Button? JoinButton { get; set; }
    [Export] public Label? DetailLabel { get; set; }
    [Export] public BoxContainer? RoomListContainer { get; set; }
    [Export] public PackedScene? RoomInfoScene { get; set; }
}