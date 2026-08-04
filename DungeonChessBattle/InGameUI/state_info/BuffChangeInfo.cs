using Godot;
using System;

namespace DungeonChessBattle;

public partial class BuffChangeInfo : FadeInfo {
    public enum Enum_BuffChangeType {
        Added,
        Removed,
    }

    [ExportGroup("Internal")]
    [Export]
    Label? label_ChangeRef;
    [Export]
    TextureRect? textureRectRef;

    public override void _Ready() {
        base._Ready();
        if (label_ChangeRef == null)
            GD.PrintErr("[BuffChangeInfo] [Export] label_ChangeRef is not assigned!");
        if (textureRectRef == null)
            GD.PrintErr("[BuffChangeInfo] [Export] textureRectRef is not assigned!");
    }

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

    public override void _Process(double delta) {
        UpdateFade(delta);
    }
}
