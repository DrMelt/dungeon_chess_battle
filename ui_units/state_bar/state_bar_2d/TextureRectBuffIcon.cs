using GameLogic;
using Godot;

namespace DungeonChessBattle {

    public partial class TextureRectBuffIcon : TextureRect {
        [Export]
        Color fromFocusUnit = new(0.3f, 0.9f, 0.3f, 1);
        [Export]
        Color fromOther = new(0.8f, 0.8f, 0.8f, 1);

        [ExportGroup("Internal Parameters")]
        [Export]
        Label superpositionsLabelRef;
        [Export]
        Label durationLabelRef;


        BuffBase bindingBuff;
        public BuffBase BindingBuff => bindingBuff;

        public void SetBuffIcon(BuffBase buffBase, UnitState focusUnit) {
            bindingBuff = buffBase;
            Color fontColor = fromOther;
            if (buffBase.fromUnit == focusUnit) {
                fontColor = fromFocusUnit;
            }

            durationLabelRef.Text = buffBase.duration.ToString("F0");
            superpositionsLabelRef.Text = buffBase.superpositions.ToString();

            durationLabelRef.LabelSettings.FontColor = fontColor;

            Texture = buffBase.icon;
        }
    }
}
