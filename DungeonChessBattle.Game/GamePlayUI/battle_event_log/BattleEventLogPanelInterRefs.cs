using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// BattleEventLogPanel 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class BattleEventLogPanelInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleEventLogPanelInterRefs> _logger = ServiceLocator.GetLogger<BattleEventLogPanelInterRefs>();

    /// <summary>日志文本显示控件。</summary>
    [Export]
    public RichTextLabel? LogLabelRef {
        get; set;
    }

    /// <summary>节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。</summary>
    public override void _Ready() {
        if (LogLabelRef == null)
            _logger.LogError("LogLabelRef is not assigned!");
    }
}
