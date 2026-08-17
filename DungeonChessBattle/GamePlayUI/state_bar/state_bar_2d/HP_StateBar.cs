using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Entities;
using DungeonChessBattle.MainScene;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 2D 血条组件，直读 UnitPawn 同步值展示单位生命值、护盾与阵营关系颜色。
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
    /// 根据单位 Pawn 刷新血条数值、百分比、阵营关系颜色与名称。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    /// <param name="session">战斗会话上下文，用于解析目标相对本地玩家的阵营关系；未就绪时不更新颜色。</param>
    public void UpdateUI_WithUnit(UnitPawn pawn, BattleSessionContext? session) {
        if (pawn == null || InterRefs == null) {
            return;
        }

        var maxHealth = Mathf.Max(pawn.MaxHealth.Value, 1f);
        var healthPercent = Mathf.Clamp(pawn.Health.Value / maxHealth, 0f, 1f);

        var progressBar = InterRefs.ProgressBarRef;
        if (progressBar != null) {
            progressBar.Value = healthPercent;
            var uiSettings = InterRefs.PlayerUISettingsRef;
            if (uiSettings != null) {
                // 未就绪/未知显式置灰，绝不投影错误的敌我色
                var relation = session != null
                    && session.TryResolveLocalCampRelation(pawn.Camp.Value, out var resolved)
                    ? resolved
                    : CampRelation.Unknown;
                progressBar.SelfModulate = uiSettings.GetRelationColor(relation);
            }
        }

        InterRefs.LabelPercentRef?.Text = healthPercent.ToString("P1");
        InterRefs.LabelCurrentValueRef?.Text = pawn.Health.Value.ToString("F1");
        InterRefs.LabelObjectNameRef?.Text = pawn.UnitName.Value;
    }

}
