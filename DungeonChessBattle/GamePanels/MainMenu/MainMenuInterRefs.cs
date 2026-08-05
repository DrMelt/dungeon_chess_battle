using Godot;

namespace DungeonChessBattle;

/// <summary>
/// MainMenu 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class MainMenuInterRefs : Node {
    /// <summary>服务器地址输入框。</summary>
    [Export]
    public LineEdit? HostInput {
        get; private set;
    }
    /// <summary>服务器端口输入框。</summary>
    [Export]
    public LineEdit? PortInput {
        get; private set;
    }
    /// <summary>连接服务器按钮。</summary>
    [Export]
    public Button? ConnectButton {
        get; private set;
    }
    /// <summary>打开服务器管理面板按钮。</summary>
    [Export]
    public Button? ServerManageButton {
        get; private set;
    }
    /// <summary>连接状态提示标签。</summary>
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    /// <summary>用户名输入框。</summary>
    [Export]
    public LineEdit? UserNameInput {
        get; private set;
    }
    /// <summary>密码输入框。</summary>
    [Export]
    public LineEdit? PasswordInput {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (HostInput == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] HostInput is not assigned!");
        if (PortInput == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] PortInput is not assigned!");
        if (ConnectButton == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] ConnectButton is not assigned!");
        if (ServerManageButton == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] ServerManageButton is not assigned!");
        if (StatusLabel == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] StatusLabel is not assigned!");
        if (UserNameInput == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] UserNameInput is not assigned!");
        if (PasswordInput == null)
            GD.PrintErr("[MainMenuInterRefs] [Export] PasswordInput is not assigned!");
    }
}
