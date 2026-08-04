using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateChangeInfo 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateChangeInfoInterRefs : Node {
    [Export]
    public UserUISettings? UserUISettingsRes {
        get; private set;
    }
    [Export]
    public PackedScene? TookDamageInfoPackedScene {
        get; private set;
    }
    [Export]
    public PackedScene? BuffChangeInfoPackedScene {
        get; private set;
    }

    public override void _Ready() {
        if (UserUISettingsRes == null)
            GD.PrintErr("[StateChangeInfoInterRefs] [Export] UserUISettingsRes is not assigned!");
        if (TookDamageInfoPackedScene == null)
            GD.PrintErr("[StateChangeInfoInterRefs] [Export] TookDamageInfoPackedScene is not assigned!");
        if (BuffChangeInfoPackedScene == null)
            GD.PrintErr("[StateChangeInfoInterRefs] [Export] BuffChangeInfoPackedScene is not assigned!");
    }
}
