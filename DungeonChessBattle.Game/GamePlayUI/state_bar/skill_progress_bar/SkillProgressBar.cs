using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.GameAssets;
using Godot;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 技能施法进度条，直读 Pawn.SkillCasting / SkillCastRemaining 展示当前施法技能名称、剩余时间与进度。
/// </summary>
public partial class SkillProgressBar : Control {
    /// <summary>导出引用集合节点。</summary>
    public SkillProgressBarInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<SkillProgressBarInterRefs>("SkillProgressBarInterRefs");
    }

    /// <summary>
    /// 根据单位 Pawn 刷新施法进度条；无施法时隐藏。
    /// </summary>
    /// <param name="unit">目标单位展示视图。</param>
    public void UpdateUI_WithUnit(IUnitUiView unit) {
        if (InterRefs == null)
            return;

        string castingId = unit.SkillCasting.Id;
        if (string.IsNullOrEmpty(castingId)) {
            Visible = false;
            return;
        }

        var skill = SkillResourceTable.GetResourceBySkillId(new SkillKeyId(castingId));
        if (skill == null) {
            Visible = false;
            return;
        }

        Visible = true;
        InterRefs.LabelSkillNameRef?.Text = skill.SkillName;
        var remaining = unit.SkillCastRemaining;
        InterRefs.LabelRemainingTimeRef?.Text = remaining.ToString("F1");
        var total = Mathf.Max(skill.SkillSpellTime, 0.001f);
        InterRefs.ProgressBarRef?.Value = Mathf.Clamp(1f - remaining / total, 0f, 1f);
    }
}
