using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.ReplayUI;

/// <summary>
/// ReplayItem 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class ReplayItemInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ReplayItemInterRefs> _logger = ServiceLocator.GetLogger<ReplayItemInterRefs>();

    /// <summary>回放摘要文本标签。</summary>
    [Export]
    public Label? InfoLabel {
        get; private set;
    }
    /// <summary>行右侧下载按钮，只负责获取副本，语义由 ReplayPanel 决定。</summary>
    [Export]
    public Button? ActionButton {
        get; private set;
    }
    /// <summary>行右侧播放按钮，显式启动回放。</summary>
    [Export]
    public Button? PlayButton {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (InfoLabel == null)
            _logger.LogError("InfoLabel is not assigned!");
        if (ActionButton == null)
            _logger.LogError("ActionButton is not assigned!");
        if (PlayButton == null)
            _logger.LogError("PlayButton is not assigned!");
    }
}
