using GameLogic;
using Godot;
using System;

public partial class BuffChangeInfo : FadeInfo {
    public enum Enum_BuffChangeType {
        Added,
        Removed,
    }

    [ExportGroup("Internal")]
    [Export]
    Label label_ChangeRef;
    [Export]
    TextureRect textureRectRef;
    public void Init(BuffBase buffBase, Enum_BuffChangeType changeType) {
        switch (changeType) {
            case Enum_BuffChangeType.Added:
                label_ChangeRef.Text = "+";
                break;
            case Enum_BuffChangeType.Removed:
                label_ChangeRef.Text = "-";
                break;
            default:
                throw new NotImplementedException();
        }

        textureRectRef.Texture = buffBase.icon;
    }

    public override void _Process(double delta) {
        UpdateFade(delta);
    }
}
