using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

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

    /// <summary>当前绑定的 Buff 展示数据。</summary>
    public IBuffUiView? BindingBuffData {
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

    /// <summary>当前绑定的焦点单位展示视图，用于判断 Buff 来源颜色。</summary>
    private IUnitUiView? _focusUnit;

    /// <summary>
    /// 绑定并展示 Buff 信息：设置图标、持续时间、层数及来源颜色。
    /// 同单位且关键字段未变化时跳过，供缓存同步器每帧刷新复用。
    /// </summary>
    /// <param name="buff">要展示的 Buff 视图。</param>
    /// <param name="focusUnit">当前焦点单位，用于判断 Buff 来源颜色。</param>
    public void SetBuffIcon(IBuffUiView buff, IUnitUiView focusUnit) {
        bool contentChanged = !(_focusUnit == focusUnit && SameContent(buff));
        BindingBuffData = buff;
        _focusUnit = focusUnit;

        // 剩余时间标签由 _Process 每帧刷新，不参与短路；仅内容变化时重设图标/层数/颜色。
        if (!contentChanged)
            return;

        if (durationLabelRef == null || superpositionsLabelRef == null)
            return;

        UpdateDurationLabel();
        superpositionsLabelRef.Text = buff.Stacks.ToString();

        durationLabelRef.LabelSettings.FontColor =
            buff.FromNetId == focusUnit.UnitId ? fromFocusUnit : fromOther;

        // 图标按 BuffTypeId 从资源表匹配；未注册时留空
        Texture = buffResourceTable?.GetResourceByBuffTypeId(buff.BuffTypeId)?.Icon;
    }

    /// <summary>仅比较决定图标外观的稳定字段；剩余时间经 _Process 每帧刷新，不纳入短路判定。</summary>
    private bool SameContent(IBuffUiView other) =>
        BindingBuffData != null
        && BindingBuffData.BuffTypeId == other.BuffTypeId
        && BindingBuffData.Stacks == other.Stacks
        && BindingBuffData.FromNetId == other.FromNetId
        && BindingBuffData.DamageType == other.DamageType;

    /// <summary>每帧按本地剩余时间刷新剩余秒数标签，平滑倒数。</summary>
    public override void _Process(double delta) {
        UpdateDurationLabel();
    }

    /// <summary>按本地 Buff 剩余时间刷新剩余秒数标签。</summary>
    private void UpdateDurationLabel() {
        if (durationLabelRef == null || _focusUnit == null || BindingBuffData == null)
            return;
        durationLabelRef.Text = BindingBuffData.Remaining.ToString("F0");
    }
}
