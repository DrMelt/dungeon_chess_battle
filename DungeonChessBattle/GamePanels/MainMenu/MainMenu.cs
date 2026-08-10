using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Core;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 主界面脚本，提供连接服务器功能。
/// 连接生命周期由 GameClientService 管理（Godot 主线程 GameClientDriver 每帧驱动），
/// 本面板仅负责 UI 交互。
/// 连接成功后切换到 GameLobby 界面。
/// </summary>
public partial class MainMenu : BaseGamePanel {
    /// <summary>日志记录器。</summary>
    private readonly ILogger<MainMenu> _logger = ServiceLocator.GetLogger<MainMenu>();

    /// <summary>服务器连接成功信号。</summary>
    [Signal]
    public delegate void ServerConnectedEventHandler();

    #region References

    /// <summary>游戏大厅界面引用，连接成功后切换显示。</summary>
    [Export]
    private GameLobby? _gameLobby;

    /// <summary>服务器管理面板引用。</summary>
    [Export]
    private ServerManagementPanel? _serverMgmtPanel;

    /// <summary>导出引用集合节点。</summary>
    public MainMenuInterRefs? InterRefs {
        get; private set;
    }

    #endregion

    /// <summary>
    /// 节点就绪：获取引用集合、绑定按钮事件、订阅客户端服务事件。
    /// </summary>
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

        _logger.LogInformation("MainMenu ready");

        // 订阅客户端服务连接状态事件
        ServiceLocator.ClientService.ConnectionChanged += OnConnectionChanged;

        // 初始隐藏 GameLobby，显示自身
        _gameLobby?.Visible = false;

        // 默认端口
        InterRefs?.PortInput?.Text = ServiceLocator.DefaultPort.ToString();
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

    /// <summary>
    /// 点击连接按钮：校验输入并调用客户端服务连接服务器。
    /// </summary>
    private void OnConnectPressed() {
        string host = InterRefs?.HostInput?.Text?.Trim() ?? "";
        string portText = InterRefs?.PortInput?.Text?.Trim() ?? "";
        string playerName = InterRefs?.UserNameInput?.Text?.Trim() ?? "";
        string password = InterRefs?.PasswordInput?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(host)) {
            UpdateStatus("请输入服务器地址");
            return;
        }
        if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535) {
            UpdateStatus("端口号无效");
            return;
        }

        if (string.IsNullOrWhiteSpace(playerName)) {
            UpdateStatus("请输入用户名");
            return;
        }
        if (playerName.Length > EntityConstants.MaxPlayerNameLength) {
            UpdateStatus($"用户名不能超过 {EntityConstants.MaxPlayerNameLength} 个字符");
            return;
        }

        // 在连接前设置身份信息
        ServiceLocator.ClientService.Configure(playerName, password);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("连接请求: {Host}:{Port}, player={PlayerName}", host, port, playerName);
        UpdateStatus($"正在连接 {host}:{port}...");
        InterRefs?.ConnectButton?.Disabled = true;

        ServiceLocator.ClientService.Connect(host, port);
    }

    /// <summary>
    /// 点击服务器管理按钮，切换到服务器管理面板。
    /// </summary>
    private void OnServerManagePressed() {
        NavigateTo(_serverMgmtPanel);
    }

    #endregion

    #region Service Event Handlers

    /// <summary>
    /// 连接状态变更回调，延迟到帧末统一处理 UI。
    /// </summary>
    /// <param name="host">服务器地址。</param>
    /// <param name="port">服务器端口。</param>
    /// <param name="connected">是否已连接。</param>
    private void OnConnectionChanged(string host, int port, bool connected) {
        // 使用 CallDeferred 确保 UI 操作在 Godot 主线程安全阶段执行
        CallDeferred(nameof(DeferredConnectionChanged), host, port, connected);
    }

    /// <summary>
    /// 在主线程处理连接状态变更：连接成功切换到游戏大厅，断开则恢复按钮。
    /// </summary>
    /// <param name="host">服务器地址。</param>
    /// <param name="port">服务器端口。</param>
    /// <param name="connected">是否已连接。</param>
    private void DeferredConnectionChanged(string host, int port, bool connected) {
        if (connected) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("连接成功: {Host}:{Port}", host, port);
            UpdateStatus($"已连接到 {host}:{port}");
            EmitSignal(SignalName.ServerConnected);
            // 切换界面：隐藏主菜单，显示大厅
            NavigateTo(_gameLobby);
        }
        else {
            _logger.LogWarning("连接断开: {Host}:{Port}", host, port);
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

    /// <summary>
    /// 更新状态栏文字。
    /// </summary>
    /// <param name="message">要显示的状态信息。</param>
    private void UpdateStatus(string message) {
        InterRefs?.StatusLabel?.Text = message;
    }

    #endregion

    /// <summary>
    /// 节点退出场景树时取消订阅连接状态事件。
    /// </summary>
    public override void _ExitTree() {
        ServiceLocator.ClientService.ConnectionChanged -= OnConnectionChanged;
    }
}
