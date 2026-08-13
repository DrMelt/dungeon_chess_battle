using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// ContainerBuffs 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class ContainerBuffsInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ContainerBuffsInterRefs> _logger = ServiceLocator.GetLogger<ContainerBuffsInterRefs>();

    /// <summary>Buff 图标使用的场景资源。</summary>
    [Export]
    public PackedScene? BuffIconPackedScene {
        get; set;
    }

    /// <summary>Buff 图标横向流式布局容器。</summary>
    [Export]
    public HFlowContainer? BuffContainer {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (BuffIconPackedScene == null) {
            _logger.LogError("BuffIconPackedScene is not assigned!");
        }
        if (BuffContainer == null) {
            _logger.LogError("BuffContainer is not assigned!");
        }
    }
}
