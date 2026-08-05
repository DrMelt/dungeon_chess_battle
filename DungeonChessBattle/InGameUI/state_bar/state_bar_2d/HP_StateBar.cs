using System.Linq;
using DungeonChessBattle.InGameUI.ui_interface;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 2D 血条组件，展示单位生命值、护盾与阵营颜色。
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
    /// 根据单位状态刷新血条数值、百分比、阵营颜色与名称。
    /// </summary>
    /// <param name="unitState">目标单位状态。</param>
    public void UpdateUI_WithUnit(UnitState unitState) {
        if (unitState == null || InterRefs == null) {
            return;
        }

        var progressBar = InterRefs.ProgressBarRef;
        if (progressBar != null) {
            Color? campColor = InterRefs.PlayerUISettingsRef?.GetCampColor(unitState.Camps.FirstOrDefault() ?? "");
            progressBar.SelfModulate = campColor ?? Colors.White;
            progressBar.Value = unitState.Health_Percent;
        }

        InterRefs.LabelPercentRef?.Text = unitState.Health_Shield_Percent.ToString("P1");
        InterRefs.LabelCurrentValueRef?.Text = unitState.Health_Shield.ToString("F1");
        InterRefs.LabelObjectNameRef?.Text = unitState.UnitStateName;
    }

}
