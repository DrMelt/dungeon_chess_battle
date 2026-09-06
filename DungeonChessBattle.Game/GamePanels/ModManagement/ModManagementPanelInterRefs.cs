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

    /// <summary>mod 行容器，面板按扫描结果重建其子节点。</summary>
    [Export]
    public VBoxContainer? ModList {
        get; private set;
    }
    /// <summary>启用集与指纹摘要标签。</summary>
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    /// <summary>装载错误标签，无错误时隐藏。</summary>
    [Export]
    public Label? ErrorLabel {
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
        if (ModList == null)
            _logger.LogError("ModList is not assigned!");
        if (StatusLabel == null)
            _logger.LogError("StatusLabel is not assigned!");
        if (ErrorLabel == null)
            _logger.LogError("ErrorLabel is not assigned!");
        if (RescanButton == null)
            _logger.LogError("RescanButton is not assigned!");
        if (OpenFolderButton == null)
            _logger.LogError("OpenFolderButton is not assigned!");
        if (CloseButton == null)
            _logger.LogError("CloseButton is not assigned!");
    }
}
