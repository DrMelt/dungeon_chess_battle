using Godot;
using DungeonChessBattle.Client;

namespace DungeonChessBattle;

/// <summary>
/// 主界面脚本，提供连接服务器功能。
/// 连接成功后切换到 GameLobby 界面。
/// </summary>
public partial class MainMenu : Control {
    [Signal]
    public delegate void ServerConnectedEventHandler();

    #region References

    private MainMenuInterRefs? _interRefs;
    private NetworkBattleClient? _networkClient;
    private GameLobby? _gameLobby;
    private ServerManagementPanel? _serverMgmtPanel;

    #endregion

    private const int DefaultPort = 9050;

    private PackedScene? _serverMgmtScene;

    public override void _Ready() {
        _interRefs = GetNode<MainMenuInterRefs>("MainMenuInterRefs");

        // 加载服务器管理面板场景
        _serverMgmtScene = GD.Load<PackedScene>("res://GamePanels/ServerManagement/server_management_panel.tscn");

        // 查找同级 GameLobby 节点
        _gameLobby = GetParent()?.GetNode<GameLobby>("GameLobby");

        // 连接按钮
        if (_interRefs?.ConnectButton is not null)
            _interRefs.ConnectButton.Pressed += OnConnectPressed;

        if (_interRefs?.ServerManageButton is not null)
            _interRefs.ServerManageButton.Pressed += OnServerManagePressed;

        // 初始隐藏 GameLobby，显示自身
        if (_gameLobby is not null)
            _gameLobby.Visible = false;
    }

    #region Button Handlers

    private void OnConnectPressed() {
        string host = _interRefs?.HostInput?.Text?.Trim() ?? "";
        string portText = _interRefs?.PortInput?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(host)) {
            UpdateStatus("请输入服务器地址");
            return;
        }
        if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535) {
            UpdateStatus("端口号无效");
            return;
        }

        UpdateStatus($"正在连接 {host}:{port}...");
        if (_interRefs?.ConnectButton is not null)
            _interRefs.ConnectButton.Disabled = true;

        try {
            _networkClient = new NetworkBattleClient();
            _networkClient.Connect(host, port);

            var provider = BattleServiceProvider.CreateNetwork(_networkClient);
            _gameLobby?.SetClientService(provider.ClientService);

            UpdateStatus($"已连接到 {host}:{port}");

            // 切换界面：隐藏主菜单，显示大厅
            Visible = false;
            if (_gameLobby is not null)
                _gameLobby.Visible = true;

            EmitSignal(SignalName.ServerConnected);
            GD.Print($"[MainMenu] Connected to server: {host}:{port}");
        }
        catch (System.Exception ex) {
            UpdateStatus($"连接失败: {ex.Message}");
            if (_interRefs?.ConnectButton is not null)
                _interRefs.ConnectButton.Disabled = false;
            GD.PrintErr($"[MainMenu] Connection failed: {ex.Message}");
        }
    }

    private void OnServerManagePressed() {
        if (_serverMgmtScene == null)
            return;

        // 如果已有面板实例则复用，否则创建
        if (_serverMgmtPanel == null) {
            _serverMgmtPanel = _serverMgmtScene.Instantiate<ServerManagementPanel>();
            // 挂到 Interface 节点下（MainMenu 的父节点）
            GetParent()?.AddChild(_serverMgmtPanel);
        }

        _serverMgmtPanel.Visible = true;
    }

    #endregion

    #region Helpers

    private void UpdateStatus(string message) {
        if (_interRefs?.StatusLabel is not null)
            _interRefs.StatusLabel.Text = message;
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