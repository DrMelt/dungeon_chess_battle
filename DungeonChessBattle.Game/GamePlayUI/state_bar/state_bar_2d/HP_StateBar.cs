using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.BattleScene;
using Godot;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 2D 血条组件，直读单位展示视图展示生命值、百分比与阵营关系颜色。
/// </summary>
public partial class HP_StateBar : Control {
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
    /// 根据单位展示视图刷新血条数值、百分比、阵营关系颜色与名称。
    /// </summary>
    /// <param name="unit">目标单位展示视图。</param>
    /// <param name="session">战斗会话上下文，用于解析目标相对本地玩家的阵营关系；未就绪时置灰为未知色。</param>
    public void UpdateUI_WithUnit(IUnitUiView unit, BattleSessionContext? session) {
        if (InterRefs == null) {
            return;
        }

        var maxHealth = Mathf.Max(unit.MaxHealth, 1f);
        var healthPercent = Mathf.Clamp(unit.Health / maxHealth, 0f, 1f);

        var progressBar = InterRefs.ProgressBarRef;
        if (progressBar != null) {
            progressBar.Value = healthPercent;
            var uiSettings = InterRefs.PlayerUISettingsRef;
            if (uiSettings != null) {
                // 未就绪/未知显式置灰，绝不投影错误的敌我色
                var relation = session?.ResolveLocalCampRelation(unit.Camps) ?? CampRelation.Unknown;
                progressBar.SelfModulate = uiSettings.GetRelationColor(relation);
            }
        }

        InterRefs.LabelPercentRef?.Text = healthPercent.ToString("P1");
        InterRefs.LabelCurrentValueRef?.Text = unit.Health.ToString("F1");
        InterRefs.LabelObjectNameRef?.Text = unit.UnitName;
    }

}
