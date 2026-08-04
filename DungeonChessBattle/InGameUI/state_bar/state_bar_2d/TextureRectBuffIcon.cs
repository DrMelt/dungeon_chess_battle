using System;
using Godot;

namespace DungeonChessBattle;

public partial class TextureRectBuffIcon : TextureRect {
    [Export]
    Color fromFocusUnit = new(0.3f, 0.9f, 0.3f, 1);
    [Export]
    Color fromOther = new(0.8f, 0.8f, 0.8f, 1);

    [ExportGroup("Internal Parameters")]
    [Export]
    Label? superpositionsLabelRef;
    [Export]
    Label? durationLabelRef;

    BuffBaseGodot? bindingBuff;
    public BuffBaseGodot BindingBuff => bindingBuff ?? throw new InvalidOperationException("BindingBuff has not been set.");

    public override void _Ready() {
        if (superpositionsLabelRef == null)
            GD.PrintErr("[TextureRectBuffIcon] [Export] superpositionsLabelRef is not assigned!");
        if (durationLabelRef == null)
            GD.PrintErr("[TextureRectBuffIcon] [Export] durationLabelRef is not assigned!");
    }

    public void SetBuffIcon(BuffBaseGodot buffBase, UnitState focusUnit) {
        bindingBuff = buffBase;
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
