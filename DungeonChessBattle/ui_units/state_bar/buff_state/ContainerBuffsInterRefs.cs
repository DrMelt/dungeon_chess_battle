using Godot;

namespace DungeonChessBattle;

/// <summary>
/// ContainerBuffs 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class ContainerBuffsInterRefs : Node {
    [Export]
    public PackedScene? BuffIconPackedScene {
        get => _buffIconPackedScene;
        set => _buffIconPackedScene = value;
    }

    [Export]
    public HFlowContainer? BuffContainer {
        get => _buffContainer;
        set => _buffContainer = value;
    }


    private PackedScene? _buffIconPackedScene;
    private HFlowContainer? _buffContainer;

    public override void _Ready() {
        if (_buffIconPackedScene == null) {
            GD.PrintErr("[ContainerBuffsInterRefs] [Export] BuffIconPackedScene is not assigned!");
        }
        if (_buffContainer == null) {
            GD.PrintErr("[ContainerBuffsInterRefs] [Export] BuffContainer is not assigned!");
        }
    }
}
