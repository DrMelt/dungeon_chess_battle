using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateBarList 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBarListInterRefs : Node {
    [Export]
    public string ListOfCamp { get; set; } = "";

    [Export]
    public VBoxContainer? VBoxContainerRef {
        get; set;
    }

    [Export]
    public PackedScene? StateBarMiniPKS {
        get; set;
    }

    public override void _Ready() {
        if (VBoxContainerRef == null)
            GD.PrintErr("[StateBarListInterRefs] [Export] VBoxContainerRef is not assigned!");
        if (StateBarMiniPKS == null)
            GD.PrintErr("[StateBarListInterRefs] [Export] StateBarMiniPKS is not assigned!");
        if (string.IsNullOrEmpty(ListOfCamp))
            GD.PrintErr("[StateBarListInterRefs] [Export] ListOfCamp is still empty!");
    }
}
