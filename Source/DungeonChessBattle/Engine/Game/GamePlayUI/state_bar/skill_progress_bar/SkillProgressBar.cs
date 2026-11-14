using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 技能施法进度条，直读单位数值视图展示当前施法技能名称、剩余时间与进度。
/// 施法总时长取自内容注册表的技能定义，名称取自展示取数入口，未声明展示时回退技能键。
/// </summary>
public partial class SkillProgressBar : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<SkillProgressBar> _logger = ServiceLocator.GetLogger<SkillProgressBar>();

    /// <summary>导出引用集合节点。</summary>
    public SkillProgressBarInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>已告警过的技能键：本组件每帧刷新，同一实例内同一技能只记一次。</summary>
    private readonly HashSet<SkillKeyId> _warnedMissingSkills = [];

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<SkillProgressBarInterRefs>("SkillProgressBarInterRefs");
    }

    /// <summary>
    /// 根据单位数值视图刷新施法进度条；无施法时隐藏。
    /// </summary>
    /// <param name="unit">目标单位数值视图。</param>
    public void UpdateUI_WithUnit(ICombatValuesView unit) {
        if (InterRefs == null)
            return;

        string castingId = unit.SkillCasting.Id;
        if (string.IsNullOrEmpty(castingId)) {
            Visible = false;
            return;
        }

        var castingKey = new SkillKeyId(castingId);
        var config = ServiceLocator.ContentRegistry.GetSkill(castingKey);
        if (config == null) {
            WarnMissingSkill(castingKey);
            Visible = false;
            return;
        }

        Visible = true;
        InterRefs.LabelSkillNameRef?.Text = ServiceLocator.ModAssets?.Registry.GetSkill(castingKey)?.DisplayLabel ?? castingKey.Id;
        var remaining = unit.SkillCastRemaining;
        InterRefs.LabelRemainingTimeRef?.Text = remaining.ToString("F1");
        var total = Mathf.Max(config.SpellTime, 0.001f);
        InterRefs.ProgressBarRef?.Value = Mathf.Clamp(1f - remaining / total, 0f, 1f);
    }

    /// <summary>技能键在内容注册表查不到时记一次告警：单位引用的技能未注册，无定义可算总时长，进度条不展示。</summary>
    private void WarnMissingSkill(SkillKeyId skillKey) {
        if (!_warnedMissingSkills.Add(skillKey))
            return;
        _logger.LogWarning("技能 '{SkillId}' 未注册，施法进度条不展示。", skillKey.Id);
    }
}
