using Godot;

namespace DungeonChessBattle;

/// <summary>
/// SkillsList 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class SkillsListInterRefs : Node {
    [Export]
    public PackedScene? SkillButtonPackedScene {
        get; private set;
    }
    [Export]
    public HBoxContainer? HBoxContainerRef {
        get; private set;
    }

    public override void _Ready() {
        if (SkillButtonPackedScene == null)
            GD.PrintErr("[SkillsListInterRefs] [Export] SkillButtonPackedScene is not assigned!");
        if (HBoxContainerRef == null)
            GD.PrintErr("[SkillsListInterRefs] [Export] HBoxContainerRef is not assigned!");
    }
}
