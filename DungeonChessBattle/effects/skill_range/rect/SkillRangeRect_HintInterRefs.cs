using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Effects;

/// <summary>
/// 矩形范围技能提示的导出引用集合节点。
/// </summary>
public partial class SkillRangeRect_HintInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<SkillRangeRect_HintInterRefs> _logger = ServiceLocator.GetLogger<SkillRangeRect_HintInterRefs>();

    /// <summary>
    /// 范围贴花网格实例引用。
    /// </summary>
    [Export]
    public MeshInstance3D? DecalRef {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验关键导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (DecalRef == null) {
            _logger.LogError("DecalRef is not assigned!");
        }
    }
}
