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
    Label label_ChangeRef;
    [Export]
    TextureRect textureRectRef;
    public void Init(DungeonChessBattle.Core.BuffBaseGodot buffBase, Enum_BuffChangeType changeType) {
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
