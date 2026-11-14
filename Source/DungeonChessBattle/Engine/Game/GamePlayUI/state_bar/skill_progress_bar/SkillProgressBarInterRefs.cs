using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// SkillProgressBar 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class SkillProgressBarInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<SkillProgressBarInterRefs> _logger = ServiceLocator.GetLogger<SkillProgressBarInterRefs>();

    /// <summary>施法进度条。</summary>
    [Export]
    public ProgressBar? ProgressBarRef {
        get; set;
    }

    /// <summary>技能名称标签。</summary>
    [Export]
    public Label? LabelSkillNameRef {
        get; set;
    }

    /// <summary>剩余施法时间标签。</summary>
    [Export]
    public Label? LabelRemainingTimeRef {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (ProgressBarRef == null)
            _logger.LogError("ProgressBarRef is not assigned!");
        if (LabelSkillNameRef == null)
            _logger.LogError("LabelSkillNameRef is not assigned!");
        if (LabelRemainingTimeRef == null)
            _logger.LogError("LabelRemainingTimeRef is not assigned!");
    }
}
