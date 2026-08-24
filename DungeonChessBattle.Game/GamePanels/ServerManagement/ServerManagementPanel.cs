using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Game.Services;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// 服务器状态管理面板，提供启动/停止游戏服务器的功能。
/// 服务器生命周期由 IServerHost 后台服务管理，本面板仅负责 UI 交互。
/// 通过主线程每帧轮询 <see cref="IServerHost.Status"/> 刷新界面（无事件订阅），
/// 从根上避免后台线程直接驱动 Godot 节点。
/// </summary>
public partial class ServerManagementPanel : BaseGamePanel {
    /// <summary>日志记录器。</summary>
    private readonly ILogger<ServerManagementPanel> _logger = ServiceLocator.GetLogger<ServerManagementPanel>();

    /// <summary>上次渲染的服务器状态，用于去重刷新。</summary>
    private ServerHostStatus _lastStatus = ServerHostStatus.Stopped;

    /// <summary>导出引用集合节点。</summary>
    public ServerManagementPanelInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：绑定按钮事件、设置默认端口并显示初始状态。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<ServerManagementPanelInterRefs>("ServerManagementPanelInterRefs");
        if (InterRefs is null) {
            _logger.LogError("ServerManagementPanelInterRefs node not found.");
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

        UpdateStatus("服务器未启动");
        _logger.LogInformation("ServerManagementPanel ready");
    }

    /// <summary>
    /// 主线程每帧轮询服务器状态，变化时刷新界面。
    /// 轮询方式避免后台线程直接驱动 UI，天然线程安全。
    /// </summary>
    /// <param name="delta">距上一帧的秒数（本处未使用）。</param>
    public override void _Process(double delta) {
        var status = ServiceLocator.ServerService.Status;
        if (status == _lastStatus)
            return;
        _lastStatus = status;
        RefreshFromStatus();
    }

    #region Button Handlers

    /// <summary>
    /// 点击启动按钮：校验端口后启动服务器。
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
    /// 点击停止按钮：停止服务器。
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

    #region UI Helpers

    /// <summary>
    /// 根据当前服务器状态刷新按钮可用性与状态文字。
    /// </summary>
    private void RefreshFromStatus() {
        UpdateButtonStates();
        switch (_lastStatus) {
            case ServerHostStatus.Running:
                UpdateStatus($"运行中 (端口 {ServiceLocator.ServerService.Port})", Colors.Green);
                break;
            case ServerHostStatus.Starting:
                UpdateStatus("启动中...", Colors.Yellow);
                break;
            default:
                string? err = ServiceLocator.ServerService.LastError;
                UpdateStatus(string.IsNullOrEmpty(err) ? "服务器已停止" : $"已停止: {err}", Colors.Gray);
                break;
        }
    }

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
}
