using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.ReplayUI;

/// <summary>
/// ReplayInputPanel 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class ReplayInputPanelInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ReplayInputPanelInterRefs> _logger = ServiceLocator.GetLogger<ReplayInputPanelInterRefs>();

    /// <summary>条目文本控件。</summary>
    [Export]
    public RichTextLabel? ListLabel {
        get; private set;
    }
    /// <summary>当前条目序号标签。</summary>
    [Export]
    public Label? CounterLabel {
        get; private set;
    }
    /// <summary>上一条条目按钮。</summary>
    [Export]
    public Button? PrevButton {
        get; private set;
    }
    /// <summary>下一条条目按钮。</summary>
    [Export]
    public Button? NextButton {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (ListLabel == null)
            _logger.LogError("ListLabel is not assigned!");
        if (CounterLabel == null)
            _logger.LogError("CounterLabel is not assigned!");
        if (PrevButton == null)
            _logger.LogError("PrevButton is not assigned!");
        if (NextButton == null)
            _logger.LogError("NextButton is not assigned!");
    }
}
