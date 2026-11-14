using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Game.GamePlayUI.state_info;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// Buff 增减提示浮字，带淡出效果，展示 Buff 图标与变化符号。
/// </summary>
public partial class BuffChangeInfo : FadeInfo {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BuffChangeInfo> _logger = ServiceLocator.GetLogger<BuffChangeInfo>();

    /// <summary>变化符号标签（+ / -）。</summary>
    [ExportGroup("Internal")]
    [Export]
    private Label? label_ChangeRef;
    /// <summary>Buff 图标显示控件。</summary>
    [Export]
    private TextureRect? textureRectRef;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        base._Ready();
        if (label_ChangeRef == null)
            _logger.LogError("label_ChangeRef is not assigned!");
        if (textureRectRef == null)
            _logger.LogError("textureRectRef is not assigned!");
    }

    /// <summary>
    /// 初始化提示内容：设置变化符号，图标按 Buff 键取自展示取数入口。
    /// </summary>
    /// <param name="buffTypeId">要展示的 Buff 键。</param>
    /// <param name="changeType">变化类型（添加/移除）。</param>
    public void Init(BuffTypeId buffTypeId, BuffChangeType changeType) {
        if (label_ChangeRef == null || textureRectRef == null)
            return;

        label_ChangeRef.Text = changeType == BuffChangeType.Added ? "+" : "-";

        // 未声明图标时保留场景配置的占位图标
        if (ServiceLocator.ModAssets?.Registry.GetBuff(buffTypeId)?.Icon is { } icon)
            textureRectRef.Texture = icon;
    }

    /// <summary>
    /// 每帧更新淡出动画。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        UpdateFade(delta);
    }
}
