using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateBarMark2d 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBarMark2dInterRefs : Node {
    [Export]
    public HP_StateBar? PanelUnitStateBarRef {
        get => _panelUnitStateBarRef;
        set => _panelUnitStateBarRef = value;
    }
    private HP_StateBar? _panelUnitStateBarRef;

    public override void _Ready() {
        if (_panelUnitStateBarRef == null)
            GD.PrintErr("[StateBarMark2dInterRefs] [Export] PanelUnitStateBarRef is not assigned!");
    }
}
