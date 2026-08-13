using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// UnitGameShow 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class UnitGameShowInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitGameShowInterRefs> _logger = ServiceLocator.GetLogger<UnitGameShowInterRefs>();

    /// <summary>单位网格实例。</summary>
    [Export]
    public MeshInstance3D? UnitMeshInstanceRef {
        get; private set;
    }
    /// <summary>单位点击交互区域。</summary>
    [Export]
    public UnitShowArea3D? UnitShowAreaRef {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (UnitMeshInstanceRef == null)
            _logger.LogError("UnitMeshInstanceRef is not assigned!");
        if (UnitShowAreaRef == null)
            _logger.LogError("UnitShowAreaRef is not assigned!");
    }
}
