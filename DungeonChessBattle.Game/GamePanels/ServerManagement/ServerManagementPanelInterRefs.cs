using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// ServerManagementPanel 的导出引用集合。
/// </summary>
public partial class ServerManagementPanelInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ServerManagementPanelInterRefs> _logger = ServiceLocator.GetLogger<ServerManagementPanelInterRefs>();

    /// <summary>服务器端口输入框。</summary>
    [Export]
    public LineEdit? PortInput {
        get; private set;
    }
    /// <summary>启动服务器按钮。</summary>
    [Export]
    public Button? StartButton {
        get; private set;
    }
    /// <summary>停止服务器按钮。</summary>
    [Export]
    public Button? StopButton {
        get; private set;
    }
    /// <summary>关闭面板按钮。</summary>
    [Export]
    public Button? CloseButton {
        get; private set;
    }
    /// <summary>服务器状态标签。</summary>
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    /// <summary>服务器密码输入框。</summary>
    [Export]
    public LineEdit? PasswordInput {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (PortInput == null)
            _logger.LogError("PortInput is not assigned!");
        if (StartButton == null)
            _logger.LogError("StartButton is not assigned!");
        if (StopButton == null)
            _logger.LogError("StopButton is not assigned!");
        if (CloseButton == null)
            _logger.LogError("CloseButton is not assigned!");
        if (StatusLabel == null)
            _logger.LogError("StatusLabel is not assigned!");
        if (PasswordInput == null)
            _logger.LogError("PasswordInput is not assigned!");
    }
}
