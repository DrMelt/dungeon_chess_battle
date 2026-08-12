using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 淡出信息基类，按曲线驱动透明度变化并在结束后自动销毁。
/// </summary>
public partial class FadeInfo : Control {
    /// <summary>透明度随时间变化的曲线。</summary>
    [Export]
    private Curve? fadeCurve;
    /// <summary>淡出速度缩放系数。</summary>
    [Export]
    private float fadeTimeScale = 1.0f;
    /// <summary>当前淡出进度时间。</summary>
    private float fadeTime = 0.0f;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (fadeCurve == null)
            GD.PrintErr("[FadeInfo] [Export] fadeCurve is not assigned!");
    }

    /// <summary>
    /// 按曲线更新透明度，淡出结束后销毁自身。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    protected void UpdateFade(double delta) {
        if (fadeCurve == null)
            return;
        fadeTime += (float)delta * fadeTimeScale;
        var value = fadeCurve.Sample(fadeTime);
        Modulate = new Color(1.0f, 1.0f, 1.0f, value);

        if (fadeTime > 1.0f) {
            QueueFree();
        }
    }
}
