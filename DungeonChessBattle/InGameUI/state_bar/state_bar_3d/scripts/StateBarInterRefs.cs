using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateBar 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBarInterRefs : Node {
    [Export]
    public UserUISettings? UserUISettingsRef {
        get => _userUISettingsRef;
        set => _userUISettingsRef = value;
    }
    private UserUISettings? _userUISettingsRef;

    [Export]
    public MeshInstance3D? StateBarRef {
        get => _stateBarRef;
        set => _stateBarRef = value;
    }
    private MeshInstance3D? _stateBarRef;

    [Export]
    public Label3D? Label3DPercentRef {
        get => _label3DPercentRef;
        set => _label3DPercentRef = value;
    }
    private Label3D? _label3DPercentRef;

    [Export]
    public Label3D? Label3DCurrentValueRef {
        get => _label3DCurrentValueRef;
        set => _label3DCurrentValueRef = value;
    }
    private Label3D? _label3DCurrentValueRef;

    [Export]
    public Label3D? Label3DNameRef {
        get => _label3DNameRef;
        set => _label3DNameRef = value;
    }
    private Label3D? _label3DNameRef;

    public override void _Ready() {
        if (_userUISettingsRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] UserUISettingsRef is not assigned!");
        if (_stateBarRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] StateBarRef is not assigned!");
        if (_label3DPercentRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] Label3DPercentRef is not assigned!");
        if (_label3DCurrentValueRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] Label3DCurrentValueRef is not assigned!");
        if (_label3DNameRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] Label3DNameRef is not assigned!");
    }
}
