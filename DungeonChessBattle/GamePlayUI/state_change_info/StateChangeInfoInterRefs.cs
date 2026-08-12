using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateChangeInfo 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateChangeInfoInterRefs : Node {
    /// <summary>玩家 UI 设置资源。</summary>
    [Export]
    public PlayerUISettings? PlayerUISettingsRes {
        get; private set;
    }
    /// <summary>受击伤害提示使用的场景资源。</summary>
    [Export]
    public PackedScene? TookDamageInfoPackedScene {
        get; private set;
    }
    /// <summary>Buff 变化提示使用的场景资源。</summary>
    [Export]
    public PackedScene? BuffChangeInfoPackedScene {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (PlayerUISettingsRes == null)
            GD.PrintErr("[StateChangeInfoInterRefs] [Export] PlayerUISettingsRes is not assigned!");
        if (TookDamageInfoPackedScene == null)
            GD.PrintErr("[StateChangeInfoInterRefs] [Export] TookDamageInfoPackedScene is not assigned!");
        if (BuffChangeInfoPackedScene == null)
            GD.PrintErr("[StateChangeInfoInterRefs] [Export] BuffChangeInfoPackedScene is not assigned!");
    }
}
