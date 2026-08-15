using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// RoomPreparation 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class RoomPreparationInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<RoomPreparationInterRefs> _logger = ServiceLocator.GetLogger<RoomPreparationInterRefs>();

    /// <summary>房间副标题：房主标签。</summary>
    [ExportGroup("Labels")]
    [Export]
    public Label? HostLabel {
        get; private set;
    }
    /// <summary>房间副标题：副本键标签，显示客户端映射表解析的副本名。</summary>
    [Export]
    public Label? DungeonNameLabel {
        get; private set;
    }
    /// <summary>房间副标题：人数标签。</summary>
    [Export]
    public Label? PlayersLabel {
        get; private set;
    }
    /// <summary>操作状态提示标签。</summary>
    [Export]
    public Label? StatusLabel {
        get; private set;
    }
    /// <summary>房间描述信息标签。</summary>
    [Export]
    public Label? InfoLabel {
        get; private set;
    }
    /// <summary>打开单位选择面板按钮。</summary>
    [ExportGroup("Buttons")]
    [Export]
    public Button? SelectUnitButton {
        get; private set;
    }
    /// <summary>开始战斗按钮。</summary>
    [Export]
    public Button? StartBattleButton {
        get; private set;
    }
    /// <summary>返回大厅按钮。</summary>
    [Export]
    public Button? BackButton {
        get; private set;
    }
    /// <summary>已选单位卡片网格容器。</summary>
    [ExportGroup("Containers")]
    [Export]
    public GridContainer? UnitCardGrid {
        get; private set;
    }
    /// <summary>单位选择面板引用。</summary>
    [ExportGroup("Panels")]
    [Export]
    public UnitSelectPanel? UnitSelectPanel {
        get; private set;
    }
    /// <summary>单个单位卡片使用的场景资源。</summary>
    [ExportGroup("Scene Resources")]
    [Export]
    public PackedScene? UnitCardScene {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (HostLabel == null)
            _logger.LogError("HostLabel is not assigned!");
        if (DungeonNameLabel == null)
            _logger.LogError("DungeonNameLabel is not assigned!");
        if (PlayersLabel == null)
            _logger.LogError("PlayersLabel is not assigned!");
        if (StatusLabel == null)
            _logger.LogError("StatusLabel is not assigned!");
        if (InfoLabel == null)
            _logger.LogError("InfoLabel is not assigned!");
        if (SelectUnitButton == null)
            _logger.LogError("SelectUnitButton is not assigned!");
        if (StartBattleButton == null)
            _logger.LogError("StartBattleButton is not assigned!");
        if (BackButton == null)
            _logger.LogError("BackButton is not assigned!");
        if (UnitCardGrid == null)
            _logger.LogError("UnitCardGrid is not assigned!");
        if (UnitSelectPanel == null)
            _logger.LogError("UnitSelectPanel is not assigned!");
        if (UnitCardScene == null)
            _logger.LogError("UnitCardScene is not assigned!");
    }
}
