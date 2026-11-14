using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// ModManagementPanel 的导出引用集合。
/// </summary>
public partial class ModManagementPanelInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ModManagementPanelInterRefs> _logger = ServiceLocator.GetLogger<ModManagementPanelInterRefs>();

    /// <summary>mod 行列表，封装行卡片挂载与空态提示的自动显隐。</summary>
    [Export]
    public ModRowList? ModRowList {
        get; private set;
    }
    /// <summary>面板摘要标签：状态主体、装载错误与操作提示一体，置于列表下方。</summary>
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    /// <summary>重新扫描 mods 目录按钮。</summary>
    [Export]
    public Button? RescanButton {
        get; private set;
    }
    /// <summary>打开 mods 目录按钮。</summary>
    [Export]
    public Button? OpenFolderButton {
        get; private set;
    }
    /// <summary>关闭面板按钮。</summary>
    [Export]
    public Button? CloseButton {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (ModRowList == null)
            _logger.LogError("ModRowList is not assigned!");
        if (StatusLabel == null)
            _logger.LogError("StatusLabel is not assigned!");
        if (RescanButton == null)
            _logger.LogError("RescanButton is not assigned!");
        if (OpenFolderButton == null)
            _logger.LogError("OpenFolderButton is not assigned!");
        if (CloseButton == null)
            _logger.LogError("CloseButton is not assigned!");
    }
}
