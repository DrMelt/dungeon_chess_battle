using Godot;
using DungeonChessBattle.Server;

namespace DungeonChessBattle;

/// <summary>
/// 服务器状态管理面板，提供启动/停止内嵌游戏服务器的功能。
/// </summary>
public partial class ServerManagementPanel : Control {
    private ServerManagementPanelInterRefs? _interRefs;
    private GameServer? _server;
    private bool _serverRunning;

    private const int DefaultPort = 9050;

    public override void _Ready() {
        _interRefs = GetNode<ServerManagementPanelInterRefs>("ServerManagementPanelInterRefs");

        if (_interRefs?.PortInput is not null)
            _interRefs.PortInput.Text = DefaultPort.ToString();

        if (_interRefs?.StartButton is not null)
            _interRefs.StartButton.Pressed += OnStartPressed;

        if (_interRefs?.StopButton is not null) {
            _interRefs.StopButton.Pressed += OnStopPressed;
            _interRefs.StopButton.Disabled = true;
        }

        if (_interRefs?.CloseButton is not null)
            _interRefs.CloseButton.Pressed += OnClosePressed;

        UpdateStatus("服务器未启动");
        AppendLog("面板已就绪");
    }

    #region Button Handlers

    private void OnStartPressed() {
        if (_serverRunning) {
            AppendLog("服务器已在运行中");
            return;
        }

        string portText = _interRefs?.PortInput?.Text?.Trim() ?? "";
        if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535) {
            AppendLog("端口号无效");
            return;
        }

        try {
            _server = new GameServer();
            _server.StartAsync(port);
            _serverRunning = true;

            UpdateButtonStates();
            UpdateStatus($"运行中 (端口 {port})", Colors.Green);
            AppendLog($"服务器已启动，监听端口 {port}");
        }
        catch (System.Exception ex) {
            AppendLog($"启动失败: {ex.Message}");
            _server = null;
            _serverRunning = false;
            UpdateButtonStates();
        }
    }

    private void OnStopPressed() {
        if (!_serverRunning || _server == null) {
            AppendLog("服务器未在运行");
            return;
        }

        try {
            _server.Stop();
            _server = null;
            _serverRunning = false;

            UpdateButtonStates();
            UpdateStatus("服务器已停止");
            AppendLog("服务器已停止");
        }
        catch (System.Exception ex) {
            AppendLog($"停止失败: {ex.Message}");
        }
    }

    private void OnClosePressed() {
        if (_serverRunning) {
            // 确认关闭对话框
            var confirm = new AcceptDialog {
                Title = "确认",
                DialogText = "服务器仍在运行中，确定要关闭面板吗？\n关闭面板将同时停止服务器。",
                Exclusive = true,
            };
            confirm.Confirmed += () => {
                StopAndHide();
                confirm.QueueFree();
            };
            confirm.Canceled += () => confirm.QueueFree();
            GetParent().AddChild(confirm);
            confirm.PopupCentered();
        }
        else {
            Visible = false;
        }
    }

    #endregion

    #region UI Helpers

    private void UpdateButtonStates() {
        if (_interRefs?.StartButton is not null)
            _interRefs.StartButton.Disabled = _serverRunning;
        if (_interRefs?.StopButton is not null)
            _interRefs.StopButton.Disabled = !_serverRunning;
        if (_interRefs?.PortInput is not null)
            _interRefs.PortInput.Editable = !_serverRunning;
    }

    private void UpdateStatus(string text, Color? color = null) {
        if (_interRefs?.StatusLabel is null)
            return;

        _interRefs.StatusLabel.Text = $"状态: {text}";
        _interRefs.StatusLabel.Modulate = color ?? Colors.Gray;
    }

    private void AppendLog(string message) {
        if (_interRefs?.LogLabel is null)
            return;

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string existing = _interRefs.LogLabel.Text;
        // 限制日志行数
        var lines = existing.Split('\n');
        if (lines.Length >= 50) {
            var keep = new string[49];
            System.Array.Copy(lines, lines.Length - 49, keep, 0, 49);
            existing = string.Join('\n', keep);
        }

        _interRefs.LogLabel.Text = string.IsNullOrEmpty(existing)
            ? $"[{timestamp}] {message}"
            : $"{existing}\n[{timestamp}] {message}";
    }

    private void StopAndHide() {
        if (_serverRunning && _server != null) {
            try {
                _server.Stop();
            }
            catch { /* 忽略停止时的异常 */ }
            _server = null;
            _serverRunning = false;
        }
        UpdateButtonStates();
        UpdateStatus("服务器已停止");
        Visible = false;
    }

    #endregion

    public override void _ExitTree() {
        if (_serverRunning && _server != null) {
            try {
                _server.Stop();
            }
            catch { /* 忽略 */ }
            _server = null;
            _serverRunning = false;
        }
    }
}