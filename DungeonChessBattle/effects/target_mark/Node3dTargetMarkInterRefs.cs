using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 目标标记节点的资源引用集合，用于集中管理场景中引用的 UI 资源与贴花。
/// </summary>
public partial class Node3dTargetMarkInterRefs : Node {
    /// <summary>
    /// 玩家界面资源。
    /// </summary>
    [Export]
    public PlayerInterfaceRes? PlayerInterfaceRes {
        get; private set;
    }

    /// <summary>
    /// 目标标记贴花引用。
    /// </summary>
    [Export]
    public Decal? TargetDecalRef {
        get; private set;
    }

    /// <summary>
    /// 默认颜色，在未匹配到阵营颜色时使用。
    /// </summary>
    [Export]
    public Color DefultColor { get; private set; } = new("ad9b24");

    /// <summary>
    /// 玩家 UI 设置资源。
    /// </summary>
    [Export]
    public PlayerUISettings? PlayerUISettingsRes {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验关键导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (PlayerInterfaceRes == null)
            GD.PrintErr("[Node3dTargetMarkInterRefs] [Export] PlayerInterfaceRes is not assigned!");
        if (TargetDecalRef == null)
            GD.PrintErr("[Node3dTargetMarkInterRefs] [Export] TargetDecalRef is not assigned!");
        if (PlayerUISettingsRes == null)
            GD.PrintErr("[Node3dTargetMarkInterRefs] [Export] PlayerUISettingsRes is not assigned!");
    }
}
