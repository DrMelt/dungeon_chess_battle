using DungeonChessBattle.Entities;
using DungeonChessBattle.GameAssets.Skills;
using DungeonChessBattle.GamePlayUI.Interfaces;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 技能施法进度条，直读 Pawn.SkillCasting / SkillCastRemaining 展示当前施法技能名称、剩余时间与进度。
/// </summary>
public partial class SkillProgressBar : Control, IUIUpdate {
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
    /// <param name="pawn">目标单位 Pawn。</param>
    public void UpdateUI_WithUnit(UnitPawn pawn) {
        if (InterRefs == null)
            return;

        var castingId = pawn.SkillCasting.Value;
        if (castingId == 0) {
            Visible = false;
            return;
        }

        var skill = SkillResourceTable.GetResourceBySkillId(castingId);
        if (skill == null) {
            Visible = false;
            return;
        }

        Visible = true;
        InterRefs.LabelSkillNameRef?.Text = skill.SkillName;
        var remaining = pawn.SkillCastRemaining.Value;
        InterRefs.LabelRemainingTimeRef?.Text = remaining.ToString("F1");
        var total = Mathf.Max(skill.SkillSpellTime, 0.001f);
        InterRefs.ProgressBarRef?.Value = Mathf.Clamp(1f - remaining / total, 0f, 1f);
    }
}
