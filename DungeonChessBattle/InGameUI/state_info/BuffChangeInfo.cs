using Godot;
using System;

namespace DungeonChessBattle;

/// <summary>
/// Buff 增减提示浮字，带淡出效果，展示 Buff 图标与变化符号。
/// </summary>
public partial class BuffChangeInfo : FadeInfo {
    /// <summary>Buff 变化类型。</summary>
    public enum Enum_BuffChangeType {
        /// <summary>Buff 被添加。</summary>
        Added,
        /// <summary>Buff 被移除。</summary>
        Removed,
    }

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
            GD.PrintErr("[BuffChangeInfo] [Export] label_ChangeRef is not assigned!");
        if (textureRectRef == null)
            GD.PrintErr("[BuffChangeInfo] [Export] textureRectRef is not assigned!");
    }

    /// <summary>
    /// 初始化提示内容：设置变化符号与 Buff 图标。
    /// </summary>
    /// <param name="buffBase">要展示的 Buff。</param>
    /// <param name="changeType">变化类型（添加/移除）。</param>
    public void Init(BuffBaseGodot buffBase, Enum_BuffChangeType changeType) {
        if (label_ChangeRef == null || textureRectRef == null)
            return;

        label_ChangeRef.Text = changeType switch {
            Enum_BuffChangeType.Added => "+",
            Enum_BuffChangeType.Removed => "-",
            _ => throw new NotImplementedException(),
        };

        textureRectRef.Texture = buffBase.icon;
    }

    /// <summary>
    /// 每帧更新淡出动画。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        UpdateFade(delta);
    }
}
