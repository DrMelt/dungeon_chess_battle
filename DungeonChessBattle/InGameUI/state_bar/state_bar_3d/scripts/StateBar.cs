using DungeonChessBattle.Entities;
using DungeonChessBattle.InGameUI.ui_interface;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 3D 状态条组件，始终面向相机展示单位血条、护盾与名称。
/// </summary>
public partial class StateBar : Node3D, IUIUpdate {
    /// <summary>基础缩放系数。</summary>
    [Export]
    private float scaleBase = 0.5f;
    /// <summary>随相机距离增加的缩放系数。</summary>
    [Export]
    private float scaleCamera = 0.3f;

    /// <summary>导出引用集合节点。</summary>
    public StateBarInterRefs? InterRefs {
        get; private set;
    }
    /// <summary>血条着色器材质，用于设置进度与阵营颜色。</summary>
    private ShaderMaterial? stateBarMat;

    /// <summary>
    /// 节点就绪：获取引用集合并缓存血条着色器材质。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<StateBarInterRefs>("StateBarInterRefs");
        if (InterRefs?.StateBarRef?.MaterialOverride is ShaderMaterial mat) {
            stateBarMat = mat;
        }
    }

    /// <summary>
    /// 每帧让状态条面向相机并按距离调整缩放。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        LookAtCamera();
    }

    /// <summary>
    /// 面向相机并根据相机距离动态调整缩放，保持可见尺寸稳定。
    /// </summary>
    private void LookAtCamera() {
        Camera3D camera3D = GetViewport().GetCamera3D();
        if (camera3D != null) {
            Vector3 cameraPos = camera3D.GlobalPosition;
            LookAt(cameraPos, camera3D.Basis.Y);

            float cameraLen = (cameraPos - GlobalPosition).Length();
            float newScale = cameraLen * scaleCamera + scaleBase;
            Scale = new Vector3(newScale, newScale, 1);
        }
    }

    /// <summary>
    /// 根据单位 Pawn 刷新血条进度、阵营颜色、百分比、数值与名称。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    public void UpdateUI_WithUnit(UnitPawn pawn) {
        if (pawn == null || InterRefs == null) {
            return;
        }

        var maxHealth = Mathf.Max(pawn.MaxHealth.Value, 1f);
        var healthPercent = Mathf.Clamp(pawn.Health.Value / maxHealth, 0f, 1f);

        if (stateBarMat != null) {
            Color? campColor = InterRefs.PlayerUISettingsRef?.GetCampColor(pawn.Camp.Value);
            if (campColor != null) {
                stateBarMat.SetShaderParameter("ParPin_01_Color", (Color)campColor);
            }
            stateBarMat.SetShaderParameter("ParPin_01", healthPercent);
        }

        InterRefs.Label3DPercentRef?.Text = healthPercent.ToString("P1");
        InterRefs.Label3DCurrentValueRef?.Text = pawn.Health.Value.ToString("F1");
        InterRefs.Label3DNameRef?.Text = pawn.UnitName.Value;
    }

}
