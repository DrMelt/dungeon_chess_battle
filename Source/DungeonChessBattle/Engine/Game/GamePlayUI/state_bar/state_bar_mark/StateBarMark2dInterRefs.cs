using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// StateBarMark2d 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBarMark2dInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<StateBarMark2dInterRefs> _logger = ServiceLocator.GetLogger<StateBarMark2dInterRefs>();

    /// <summary>单位血条状态组件。</summary>
    [Export]
    public HP_StateBar? PanelUnitStateBarRef {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (PanelUnitStateBarRef == null)
            _logger.LogError("PanelUnitStateBarRef is not assigned!");
    }
}
