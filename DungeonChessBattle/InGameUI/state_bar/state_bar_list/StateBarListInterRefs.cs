using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateBarList 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBarListInterRefs : Node {
    /// <summary>要展示的阵营标识。</summary>
    [Export]
    public string ListOfCamp { get; set; } = "";

    /// <summary>迷你状态条纵向排列容器。</summary>
    [Export]
    public VBoxContainer? VBoxContainerRef {
        get; set;
    }

    /// <summary>迷你状态条使用的场景资源。</summary>
    [Export]
    public PackedScene? StateBarMiniPKS {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (VBoxContainerRef == null)
            GD.PrintErr("[StateBarListInterRefs] [Export] VBoxContainerRef is not assigned!");
        if (StateBarMiniPKS == null)
            GD.PrintErr("[StateBarListInterRefs] [Export] StateBarMiniPKS is not assigned!");
        if (string.IsNullOrEmpty(ListOfCamp))
            GD.PrintErr("[StateBarListInterRefs] [Export] ListOfCamp is still empty!");
    }
}
