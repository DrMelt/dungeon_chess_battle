using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateBarMini 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBarMiniInterRefs : Node {
    [Export]
    public ContainerBuffs? ContainerBuffsRef {
        get => _containerBuffsRef;
        set => _containerBuffsRef = value;
    }
    private ContainerBuffs? _containerBuffsRef;

    [Export]
    public Panel? OutlineRef {
        get => _outlineRef;
        set => _outlineRef = value;
    }
    private Panel? _outlineRef;

    [Export]
    public HP_StateBar? HpStateBarRef {
        get => _hpStateBarRef;
        set => _hpStateBarRef = value;
    }
    private HP_StateBar? _hpStateBarRef;

    [Export]
    public SkillProgressBar? SkillProgressBarRef {
        get => _skillProgressBarRef;
        set => _skillProgressBarRef = value;
    }
    private SkillProgressBar? _skillProgressBarRef;

    public override void _Ready() {
        if (_containerBuffsRef == null)
            GD.PrintErr("[StateBarMiniInterRefs] [Export] ContainerBuffsRef is not assigned!");
        if (_outlineRef == null)
            GD.PrintErr("[StateBarMiniInterRefs] [Export] OutlineRef is not assigned!");
        if (_hpStateBarRef == null)
            GD.PrintErr("[StateBarMiniInterRefs] [Export] HpStateBarRef is not assigned!");
        if (_skillProgressBarRef == null)
            GD.PrintErr("[StateBarMiniInterRefs] [Export] SkillProgressBarRef is not assigned!");
    }
}
