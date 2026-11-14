using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// StateChangeInfo 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class UnitStateChangeInfoInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitStateChangeInfoInterRefs> _logger = ServiceLocator.GetLogger<UnitStateChangeInfoInterRefs>();

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
            _logger.LogError("PlayerUISettingsRes is not assigned!");
        if (TookDamageInfoPackedScene == null)
            _logger.LogError("TookDamageInfoPackedScene is not assigned!");
        if (BuffChangeInfoPackedScene == null)
            _logger.LogError("BuffChangeInfoPackedScene is not assigned!");
    }
}
