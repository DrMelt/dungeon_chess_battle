using System;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// Buff 图标控件，展示单个 Buff 的图标、持续时间与层数，并区分来源颜色。
/// </summary>
public partial class TextureRectBuffIcon : TextureRect {
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

    /// <summary>当前绑定的 Buff 实例。</summary>
    public BuffBaseGodot BindingBuff {
        get => field ?? throw new InvalidOperationException("BindingBuff has not been set.");
        private set;
    }

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (superpositionsLabelRef == null)
            GD.PrintErr("[TextureRectBuffIcon] [Export] superpositionsLabelRef is not assigned!");
        if (durationLabelRef == null)
            GD.PrintErr("[TextureRectBuffIcon] [Export] durationLabelRef is not assigned!");
    }

    /// <summary>
    /// 绑定并展示 Buff 信息：设置图标、持续时间、层数及来源颜色。
    /// </summary>
    /// <param name="buffBase">要展示的 Buff。</param>
    /// <param name="focusUnit">当前焦点单位，用于判断 Buff 来源颜色。</param>
    public void SetBuffIcon(BuffBaseGodot buffBase, UnitState focusUnit) {
        BindingBuff = buffBase;
        Color fontColor = fromOther;
        if (buffBase.FromUnit == focusUnit) {
            fontColor = fromFocusUnit;
        }

        if (durationLabelRef == null || superpositionsLabelRef == null)
            return;

        durationLabelRef.Text = buffBase.Duration.ToString("F0");
        superpositionsLabelRef.Text = buffBase.Superpositions.ToString();

        durationLabelRef.LabelSettings.FontColor = fontColor;

        Texture = buffBase.icon;
    }
}
