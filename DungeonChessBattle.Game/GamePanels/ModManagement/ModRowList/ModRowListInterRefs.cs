using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// ModRowList 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class ModRowListInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ModRowListInterRefs> _logger = ServiceLocator.GetLogger<ModRowListInterRefs>();

    /// <summary>行卡片装填容器。</summary>
    [Export]
    public VBoxContainer? ModRows {
        get; private set;
    }
    /// <summary>列表无行时显示的占位提示。</summary>
    [Export]
    public Label? EmptyHint {
        get; private set;
    }
    /// <summary>单条 mod 行卡片使用的场景资源。</summary>
    [Export]
    public PackedScene? ItemScene {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (ModRows == null)
            _logger.LogError("ModRows is not assigned!");
        if (EmptyHint == null)
            _logger.LogError("EmptyHint is not assigned!");
        if (ItemScene == null)
            _logger.LogError("ItemScene is not assigned!");
    }
}
