using DungeonChessBattle.GamePlayUI;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Effects;

/// <summary>
/// 目标标记节点的资源引用集合，用于集中管理场景中引用的 UI 资源与贴花。
/// </summary>
public partial class Node3dTargetMarkInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<Node3dTargetMarkInterRefs> _logger = ServiceLocator.GetLogger<Node3dTargetMarkInterRefs>();

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
    /// 玩家 UI 设置资源，提供友方、中立、敌方阵营颜色。
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
            _logger.LogError("PlayerInterfaceRes is not assigned!");
        if (TargetDecalRef == null)
            _logger.LogError("TargetDecalRef is not assigned!");
        if (PlayerUISettingsRes == null)
            _logger.LogError("PlayerUISettingsRes is not assigned!");
    }
}
