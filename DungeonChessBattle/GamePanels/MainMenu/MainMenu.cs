using Godot;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 主界面脚本，提供连接服务器功能。
/// 连接生命周期由 GameClientService 后台服务管理，本面板仅负责 UI 交互。
/// 连接成功后切换到 GameLobby 界面。
/// </summary>
public partial class MainMenu : BaseGamePanel {
    [Signal]
    public delegate void ServerConnectedEventHandler();

    #region References

    [Export]
    private GameLobby? _gameLobby;

    [Export]
    private ServerManagementPanel? _serverMgmtPanel;

    public MainMenuInterRefs? InterRefs {
        get; private set;
    }

    #endregion

    public override void _Ready() {
        InterRefs = GetNode<MainMenuInterRefs>("MainMenuInterRefs");
        if (InterRefs is null) {
            GD.PrintErr("[MainMenu] MainMenuInterRefs node not found.");
            return;
        }

        // 验证所有 [Export] 字段是否已赋值
        ValidateExports();

        // 连接按钮
        InterRefs?.ConnectButton?.Pressed += OnConnectPressed;
        InterRefs?.ServerManageButton?.Pressed += OnServerManagePressed;

        // 订阅后台服务事件
        ServiceLocator.ClientService.ConnectionChanged += OnConnectionChanged;

        // 初始隐藏 GameLobby，显示自身
        _gameLobby?.Visible = false;

        // 默认端口
        InterRefs?.PortInput?.Text = ServiceLocator.DefaultPort.ToString();

        // 初始化本地模式（在没有网络连接时提供本地服务）
        ServiceLocator.ClientService.InitLocalMode();
    }

    /// <summary>
    /// 面板重新显示时恢复连接按钮状态。
    /// 解决从 GameLobby / ServerManagementPanel 返回时按钮变灰无法点击的问题。
    /// </summary>
    protected override void OnPanelOpened() {
        // 返回主界面时断开现有连接，允许用户重新连接
        if (ServiceLocator.ClientService.IsConnected) {
            ServiceLocator.ClientService.Disconnect();
        }
        InterRefs?.ConnectButton?.Disabled = false;
    }

    #region Button Handlers

    private void OnConnectPressed() {
        string host = InterRefs?.HostInput?.Text?.Trim() ?? "";
        string portText = InterRefs?.PortInput?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(host)) {
            UpdateStatus("请输入服务器地址");
            return;
        }
        if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535) {
            UpdateStatus("端口号无效");
            return;
        }

        UpdateStatus($"正在连接 {host}:{port}...");
        InterRefs?.ConnectButton?.Disabled = true;

        ServiceLocator.ClientService.Connect(host, port);
    }

    private void OnServerManagePressed() {
        NavigateTo(_serverMgmtPanel);
    }

    #endregion

    #region Service Event Handlers

    private void OnConnectionChanged(string host, int port, bool connected) {
        // 使用 CallDeferred 确保 UI 操作在 Godot 主线程执行
        // （ConnectionChanged 可能从后台线程 GameClient-Update 触发）
        CallDeferred(nameof(DeferredConnectionChanged), host, port, connected);
    }

    private void DeferredConnectionChanged(string host, int port, bool connected) {
        if (connected) {
            UpdateStatus($"已连接到 {host}:{port}");
            EmitSignal(SignalName.ServerConnected);
            // 切换界面：隐藏主菜单，显示大厅
            NavigateTo(_gameLobby);
        }
        else {
            UpdateStatus("连接已断开");
            if (InterRefs?.ConnectButton != null)
                InterRefs.ConnectButton.Disabled = false;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 验证所有 [Export] 导出字段是否已在 Godot 编辑器中正确赋值。
    /// 如果未赋值，打印错误日志以辅助排查"点击无反应"类问题。
    /// </summary>
    private void ValidateExports() {
        if (_gameLobby == null) {
            GD.PrintErr("[MainMenu] [Export] _gameLobby is not assigned!");
        }
        if (_serverMgmtPanel == null) {
            GD.PrintErr("[MainMenu] [Export] _serverMgmtPanel is not assigned!");
        }
    }

    private void UpdateStatus(string message) {
        InterRefs?.StatusLabel?.Text = message;
    }

    #endregion

    public override void _ExitTree() {
        ServiceLocator.ClientService.ConnectionChanged -= OnConnectionChanged;
    }
}
