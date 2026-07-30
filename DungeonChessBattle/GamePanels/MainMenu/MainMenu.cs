using Godot;
using DungeonChessBattle.Client;

namespace DungeonChessBattle;

/// <summary>
/// 主界面脚本，提供连接服务器功能。
/// 连接成功后切换到 GameLobby 界面。
/// </summary>
public partial class MainMenu : BaseGamePanel {
    [Signal]
    public delegate void ServerConnectedEventHandler();

    #region References

    private NetworkBattleClient? _networkClient;

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

        // 初始隐藏 GameLobby，显示自身
        _gameLobby?.Visible = false;
    }

    /// <summary>
    /// 面板重新显示时恢复连接按钮状态。
    /// 解决从 GameLobby / ServerManagementPanel 返回时按钮变灰无法点击的问题。
    /// </summary>
    protected override void OnPanelOpened() {
        if (InterRefs?.ConnectButton != null) {
            InterRefs.ConnectButton.Disabled = false;
        }
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

        try {
            _networkClient = new NetworkBattleClient();
            _networkClient.Connect(host, port);

            var provider = BattleServiceProvider.CreateNetwork(_networkClient);
            _gameLobby?.SetClientService(provider.ClientService);

            UpdateStatus($"已连接到 {host}:{port}");

            // 切换界面：隐藏主菜单，显示大厅
            NavigateTo(_gameLobby);

            EmitSignal(SignalName.ServerConnected);
            GD.Print($"[MainMenu] Connected to server: {host}:{port}");
        }
        catch (System.Exception ex) {
            UpdateStatus($"连接失败: {ex.Message}");
            InterRefs?.ConnectButton?.Disabled = false;
            GD.PrintErr($"[MainMenu] Connection failed: {ex.Message}");
        }
    }

    private void OnServerManagePressed() {
        NavigateTo(_serverMgmtPanel);
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

    /// <summary>
    /// 每帧更新网络客户端以处理实体同步。
    /// </summary>
    public override void _Process(double delta) {
        _networkClient?.Update((float)delta);
    }

    public override void _ExitTree() {
        _networkClient?.Disconnect();
    }
}
