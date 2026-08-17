using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.GameAssets;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// Buff 图标控件，展示单个 Buff 的图标、持续时间与层数，并区分来源颜色。
/// 数据源为同步 Buff 数据（SyncBuffData），图标按 BuffTypeId 从 BuffResourceTable 匹配。
/// </summary>
public partial class TextureRectBuffIcon : TextureRect {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<TextureRectBuffIcon> _logger = ServiceLocator.GetLogger<TextureRectBuffIcon>();

    /// <summary>Buff 资源表，用于按 BuffTypeId 匹配图标。</summary>
    [Export]
    private BuffResourceTable? buffResourceTable;

    /// <summary>来自焦点单位的 Buff 文字颜色（绿色）。</summary>
    [Export]
    private Color fromFocusUnit = new(0.3f, 0.9f, 0.3f, 1);
    /// <summary>来自其他单位的 Buff 文字颜色（灰色）。</summary>
    [Export]
    private Color fromOther = new(0.8f, 0.8f, 0.8f, 1);

    /// <summary>层数标签。</summary>
    [ExportGroup("Internal Parameters")]
    [Export]
    private Label? superpositionsLabelRef;
    /// <summary>持续时间标签。</summary>
    [Export]
    private Label? durationLabelRef;

    /// <summary>当前绑定的 Buff 数据。</summary>
    public SyncBuffData BindingBuffData {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (superpositionsLabelRef == null)
            _logger.LogError("superpositionsLabelRef is not assigned!");
        if (durationLabelRef == null)
            _logger.LogError("durationLabelRef is not assigned!");
        if (buffResourceTable == null)
            _logger.LogError("buffResourceTable is not assigned!");
    }

    /// <summary>
    /// 绑定并展示 Buff 信息：设置图标、持续时间、层数及来源颜色。
    /// </summary>
    /// <param name="buffData">要展示的同步 Buff 数据。</param>
    /// <param name="focusPawn">当前焦点单位，用于判断 Buff 来源颜色。</param>
    public void SetBuffIcon(SyncBuffData buffData, UnitPawn focusPawn) {
        BindingBuffData = buffData;

        if (durationLabelRef == null || superpositionsLabelRef == null)
            return;

        durationLabelRef.Text = buffData.Remaining.ToString("F0");
        superpositionsLabelRef.Text = buffData.StackCount.ToString();

        durationLabelRef.LabelSettings.FontColor =
            buffData.SourceUnitNetId == focusPawn.Id ? fromFocusUnit : fromOther;

        // 图标按 BuffTypeId 从资源表匹配；未注册时留空
        Texture = buffResourceTable?.GetResourceByBuffTypeId(buffData.BuffTypeId)?.Icon;
    }
}
