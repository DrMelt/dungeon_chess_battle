using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 服务器状态管理面板，提供启动/停止内嵌游戏服务器的功能。
/// 服务器生命周期由 GameServerService 后台服务管理，本面板仅负责 UI 交互。
/// </summary>
public partial class ServerManagementPanel : BaseGamePanel {
    private readonly ILogger<ServerManagementPanel> _logger = ServiceLocator.GetLogger<ServerManagementPanel>();

    public ServerManagementPanelInterRefs? InterRefs {
        get; private set;
    }

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

    private void OnStartPressed() {
        if (ServiceLocator.ServerService.IsRunning) {
            return;
        }

        string portText = InterRefs?.PortInput?.Text?.Trim() ?? "";
        if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535) {
            return;
        }

        string password = InterRefs?.PasswordInput?.Text?.Trim() ?? "";

        _logger.LogInformation("启动服务器: port={Port}", port);
        ServiceLocator.ServerService.Start(port, password);
    }

    private void OnStopPressed() {
        if (!ServiceLocator.ServerService.IsRunning) {
            return;
        }

        _logger.LogInformation("停止服务器");
        ServiceLocator.ServerService.Stop();
    }

    private void OnClosePressed() {
        GoBack();
    }

    #endregion

    #region Service Event Handlers

    private void OnServerStatusChanged(bool isRunning, int port) {
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

    private void UpdateButtonStates() {
        bool running = ServiceLocator.ServerService.IsRunning;
        InterRefs?.StartButton?.Disabled = running;
        InterRefs?.StopButton?.Disabled = !running;
        InterRefs?.PortInput?.Editable = !running;
        if (InterRefs?.PasswordInput is not null)
            InterRefs.PasswordInput.Editable = !running;
    }

    private void UpdateStatus(string text, Color? color = null) {
        var label = InterRefs?.StatusLabel;
        if (label is null)
            return;

        label.Text = $"状态: {text}";
        label.Modulate = color ?? Colors.Gray;
    }


    #endregion

    public override void _ExitTree() {
        // 取消订阅，避免内存泄漏
        ServiceLocator.ServerService.StatusChanged -= OnServerStatusChanged;
    }
}
