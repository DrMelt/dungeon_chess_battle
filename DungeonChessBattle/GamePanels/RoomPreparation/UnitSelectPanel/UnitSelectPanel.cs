using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// 角色选取面板。以网格形式展示所有可用单位，用户点击选择一个职业。
/// 选择后发出 UnitSelected 信号并自动返回 RoomPreparation。
/// </summary>
public partial class UnitSelectPanel : BaseGamePanel {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitSelectPanel> _logger = ServiceLocator.GetLogger<UnitSelectPanel>();

    /// <summary>单位被选中时发出的信号，参数为单位配置键。</summary>
    [Signal]
    public delegate void UnitSelectedEventHandler(string unitConfigKey);

    /// <summary>导出引用集合节点。</summary>
    private UnitSelectPanelInterRefs? _refs;

    /// <summary>
    /// 节点就绪：获取引用集合并绑定返回按钮事件。
    /// </summary>
    public override void _Ready() {
        _refs = GetNode<UnitSelectPanelInterRefs>("UnitSelectPanelInterRefs");
        if (_refs is null) {
            _logger.LogError("UnitSelectPanelInterRefs node not found.");
            return;
        }

        _refs.BackButton?.Pressed += GoBack;
    }

    /// <summary>
    /// 面板打开时填充单位网格。
    /// </summary>
    protected override void OnPanelOpened() {
        PopulateUnitGrid();
    }

    /// <summary>
    /// 填充可用单位网格。每次打开面板时重新创建 UnitCard。
    /// </summary>
    private void PopulateUnitGrid() {
        if (_refs?.UnitCardGrid is null || _refs?.UnitCardScene is null)
            return;

        // 清空旧卡片
        foreach (Node child in _refs.UnitCardGrid.GetChildren())
            child.QueueFree();

        foreach (var entry in UnitCatalog.All) {
            var card = _refs.UnitCardScene.Instantiate<UnitCard>();
            card.SetupUnit(entry.ConfigKey, entry.DisplayName, entry.Config.MaxHealth);
            card.UnitSelected += OnCardSelected;
            _refs.UnitCardGrid.AddChild(card);
        }
    }

    /// <summary>
    /// 单位卡片选中回调：发出选中信号并关闭面板。
    /// </summary>
    /// <param name="unitConfigKey">单位配置键。</param>
    private void OnCardSelected(string unitConfigKey) {
        EmitSignal(SignalName.UnitSelected, unitConfigKey);
        ClosePanel();
    }
}
