using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// GameLobby 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class GameLobbyInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<GameLobbyInterRefs> _logger = ServiceLocator.GetLogger<GameLobbyInterRefs>();

    /// <summary>房间名输入框。</summary>
    [Export]
    public LineEdit? RoomNameInput {
        get; private set;
    }
    /// <summary>副本选择下拉框，创建房间时选定敌人与环境。</summary>
    [Export]
    public OptionButton? DungeonSelect {
        get; private set;
    }
    /// <summary>创建房间按钮。</summary>
    [Export]
    public Button? CreateButton {
        get; private set;
    }
    /// <summary>刷新房间列表按钮。</summary>
    [Export]
    public Button? RefreshButton {
        get; private set;
    }
    /// <summary>加入房间按钮。</summary>
    [Export]
    public Button? JoinButton {
        get; private set;
    }
    /// <summary>展示房间详情信息的标签。</summary>
    [Export]
    public Label? DetailLabel {
        get; private set;
    }
    /// <summary>房间列表容器。</summary>
    [Export]
    public BoxContainer? RoomListContainer {
        get; private set;
    }
    /// <summary>单个房间卡片使用的场景资源。</summary>
    [Export]
    public PackedScene? RoomInfoScene {
        get; private set;
    }
    /// <summary>返回主菜单按钮。</summary>
    [Export]
    public Button? BackButton {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (RoomNameInput == null)
            _logger.LogError("RoomNameInput is not assigned!");
        if (DungeonSelect == null)
            _logger.LogError("DungeonSelect is not assigned!");
        if (CreateButton == null)
            _logger.LogError("CreateButton is not assigned!");
        if (RefreshButton == null)
            _logger.LogError("RefreshButton is not assigned!");
        if (JoinButton == null)
            _logger.LogError("JoinButton is not assigned!");
        if (DetailLabel == null)
            _logger.LogError("DetailLabel is not assigned!");
        if (RoomListContainer == null)
            _logger.LogError("RoomListContainer is not assigned!");
        if (RoomInfoScene == null)
            _logger.LogError("RoomInfoScene is not assigned!");
        if (BackButton == null)
            _logger.LogError("BackButton is not assigned!");
    }
}
