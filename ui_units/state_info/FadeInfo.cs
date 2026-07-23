using Godot;

namespace DungeonChessBattle;

public partial class FadeInfo : Control {
    [Export]
    Curve fadeCurve;
    [Export]
    float fadeTimeScale = 1.0f;
    float fadeTime = 0.0f;
    protected void UpdateFade(double delta) {
        fadeTime += (float)delta * fadeTimeScale;
        var value = fadeCurve.Sample(fadeTime);
        Modulate = new Color(1.0f, 1.0f, 1.0f, value);

        if (fadeTime > 1.0f) {
            QueueFree();
        }
    }
}
