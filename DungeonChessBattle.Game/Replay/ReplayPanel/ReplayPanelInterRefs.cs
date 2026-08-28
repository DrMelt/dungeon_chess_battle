using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.ReplayUI;

/// <summary>
/// ReplayPanel 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class ReplayPanelInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ReplayPanelInterRefs> _logger = ServiceLocator.GetLogger<ReplayPanelInterRefs>();

    /// <summary>回放条目容器，逐行卡片挂载处。</summary>
    [Export]
    public BoxContainer? ReplayListContainer {
        get; private set;
    }
    /// <summary>单条回放卡片使用的场景资源。</summary>
    [Export]
    public PackedScene? ReplayItemScene {
        get; private set;
    }
    /// <summary>刷新回放列表按钮。</summary>
    [Export]
    public Button? RefreshButton {
        get; private set;
    }
    /// <summary>关闭回放面板按钮。</summary>
    [Export]
    public Button? CloseButton {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (ReplayListContainer == null)
            _logger.LogError("ReplayListContainer is not assigned!");
        if (ReplayItemScene == null)
            _logger.LogError("ReplayItemScene is not assigned!");
        if (RefreshButton == null)
            _logger.LogError("RefreshButton is not assigned!");
        if (CloseButton == null)
            _logger.LogError("CloseButton is not assigned!");
    }
}
