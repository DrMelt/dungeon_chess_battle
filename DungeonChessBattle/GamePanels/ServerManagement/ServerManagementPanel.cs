using Godot;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 服务器状态管理面板，提供启动/停止内嵌游戏服务器的功能。
/// 服务器生命周期由 GameServerService 后台服务管理，本面板仅负责 UI 交互。
/// </summary>
public partial class ServerManagementPanel : BaseGamePanel {
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

        ServiceLocator.ServerService.Start(port);
    }

    private void OnStopPressed() {
        if (!ServiceLocator.ServerService.IsRunning) {
            return;
        }

        ServiceLocator.ServerService.Stop();
    }

    private void OnClosePressed() {
        if (ServiceLocator.ServerService.IsRunning) {
            // 确认关闭对话框
            var confirm = new AcceptDialog {
                Title = "确认",
                DialogText = "服务器仍在运行中，确定要关闭面板吗？\n关闭面板将同时停止服务器。",
                Exclusive = true,
            };
            confirm.Confirmed += () => {
                ServiceLocator.ServerService.Stop();
                ClosePanel();
                confirm.QueueFree();
            };
            confirm.Canceled += () => confirm.QueueFree();
            GetParent().AddChild(confirm);
            confirm.PopupCentered();
        }
        else {
            ClosePanel();
        }
    }

    #endregion

    #region Service Event Handlers

    private void OnServerStatusChanged(bool isRunning, int port) {
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
