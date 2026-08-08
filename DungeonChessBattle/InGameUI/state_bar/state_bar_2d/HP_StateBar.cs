using System.Linq;
using DungeonChessBattle.Entities;
using DungeonChessBattle.InGameUI.ui_interface;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 2D 血条组件，直读 UnitPawn 同步值展示单位生命值、护盾与阵营颜色。
/// </summary>
public partial class HP_StateBar : Control, IUIUpdate {
    /// <summary>导出引用集合节点。</summary>
    public HP_StateBarInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<HP_StateBarInterRefs>("HP_StateBarInterRefs");
    }

    /// <summary>
    /// 根据单位 Pawn 刷新血条数值、百分比、阵营颜色与名称。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    public void UpdateUI_WithUnit(UnitPawn pawn) {
        if (pawn == null || InterRefs == null) {
            return;
        }

        var maxHealth = Mathf.Max(pawn.MaxHealth.Value, 1f);
        var healthPercent = Mathf.Clamp(pawn.Health.Value / maxHealth, 0f, 1f);

        var progressBar = InterRefs.ProgressBarRef;
        if (progressBar != null) {
            Color? campColor = InterRefs.PlayerUISettingsRef?.GetCampColor(pawn.Camp.Value);
            progressBar.SelfModulate = campColor ?? Colors.White;
            progressBar.Value = healthPercent;
        }

        InterRefs.LabelPercentRef?.Text = healthPercent.ToString("P1");
        InterRefs.LabelCurrentValueRef?.Text = pawn.Health.Value.ToString("F1");
        InterRefs.LabelObjectNameRef?.Text = pawn.UnitName.Value;
    }

}
