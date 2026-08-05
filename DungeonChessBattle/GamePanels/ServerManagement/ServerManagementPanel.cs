using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 服务器状态管理面板，提供启动/停止内嵌游戏服务器的功能。
/// 服务器生命周期由 GameServerHost 后台服务管理，本面板仅负责 UI 交互。
/// </summary>
public partial class ServerManagementPanel : BaseGamePanel {
    /// <summary>日志记录器。</summary>
    private readonly ILogger<ServerManagementPanel> _logger = ServiceLocator.GetLogger<ServerManagementPanel>();

    /// <summary>导出引用集合节点。</summary>
    public ServerManagementPanelInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：绑定按钮事件、设置默认端口并订阅服务器状态事件。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<ServerManagementPanelInterRefs>("ServerManagementPanelInterRefs");
        if (InterRefs is null) {
            GD.PrintErr("[ServerManagementPanel] ServerManagementPanelInterRefs node not found.");
            return;
        }

        InterRefs?.PortInput?.Text = ServiceLocator.DefaultPort.ToString();
        InterRefs?.StartButton?.Pressed += OnStartPressed;
        var stopBtn = InterRefs?.StopButton;
        if (stopBtn is not null) {
            stopBtn.Pressed += OnStopPressed;
            stopBtn.Disabled = true;
        }
        InterRefs?.CloseButton?.Pressed += OnClosePressed;

        // 订阅后台服务事件
        ServiceLocator.ServerService.StatusChanged += OnServerStatusChanged;

        UpdateStatus("服务器未启动");
        _logger.LogInformation("ServerManagementPanel ready");
    }

    #region Button Handlers

    /// <summary>
    /// 点击启动按钮：校验端口后启动内嵌服务器。
    /// </summary>
    private void OnStartPressed() {
        if (ServiceLocator.ServerService.IsRunning) {
            return;
        }

        string portText = InterRefs?.PortInput?.Text?.Trim() ?? "";
        if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535) {
            return;
        }

        string password = InterRefs?.PasswordInput?.Text?.Trim() ?? "";

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("启动服务器: port={Port}", port);
        ServiceLocator.ServerService.Start(port, password);
    }

    /// <summary>
    /// 点击停止按钮：停止内嵌服务器。
    /// </summary>
    private void OnStopPressed() {
        if (!ServiceLocator.ServerService.IsRunning) {
            return;
        }

        _logger.LogInformation("停止服务器");
        ServiceLocator.ServerService.Stop();
    }

    /// <summary>
    /// 点击关闭按钮，返回上一面板。
    /// </summary>
    private void OnClosePressed() {
        GoBack();
    }

    #endregion

    #region Service Event Handlers

    /// <summary>
    /// 服务器状态变更回调，刷新按钮状态与状态文字。
    /// </summary>
    /// <param name="isRunning">服务器是否运行中。</param>
    /// <param name="port">服务器端口。</param>
    private void OnServerStatusChanged(bool isRunning, int port) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("服务器状态变更: isRunning={IsRunning}, port={Port}", isRunning, port);
        UpdateButtonStates();
        if (isRunning) {
            UpdateStatus($"运行中 (端口 {port})", Colors.Green);
        }
        else {
            UpdateStatus("服务器已停止");
        }
    }

    #endregion

    #region UI Helpers

    /// <summary>
    /// 根据服务器运行状态刷新按钮可用性与输入框可编辑性。
    /// </summary>
    private void UpdateButtonStates() {
        bool running = ServiceLocator.ServerService.IsRunning;
        InterRefs?.StartButton?.Disabled = running;
        InterRefs?.StopButton?.Disabled = !running;
        InterRefs?.PortInput?.Editable = !running;
        if (InterRefs?.PasswordInput is not null)
            InterRefs.PasswordInput.Editable = !running;
    }

    /// <summary>
    /// 更新状态标签文字与颜色。
    /// </summary>
    /// <param name="text">状态描述。</param>
    /// <param name="color">状态颜色，默认灰色。</param>
    private void UpdateStatus(string text, Color? color = null) {
        var label = InterRefs?.StatusLabel;
        if (label is null)
            return;

        label.Text = $"状态: {text}";
        label.Modulate = color ?? Colors.Gray;
    }


    #endregion

    /// <summary>
    /// 节点退出场景树时取消订阅服务器状态事件。
    /// </summary>
    public override void _ExitTree() {
        // 取消订阅，避免内存泄漏
        ServiceLocator.ServerService.StatusChanged -= OnServerStatusChanged;
    }
}
