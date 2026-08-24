using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// ClickableStateBar 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class ClickableStateBarInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ClickableStateBarInterRefs> _logger = ServiceLocator.GetLogger<ClickableStateBarInterRefs>();

    /// <summary>Buff 图标容器组件。</summary>
    [Export]
    public ContainerBuffs? ContainerBuffsRef {
        get; set;
    }

    /// <summary>悬停高亮外框。</summary>
    [Export]
    public Panel? OutlineRef {
        get; set;
    }

    /// <summary>血条状态组件。</summary>
    [Export]
    public HP_StateBar? HpStateBarRef {
        get; set;
    }

    /// <summary>施法进度条组件。</summary>
    [Export]
    public SkillProgressBar? SkillProgressBarRef {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (ContainerBuffsRef == null)
            _logger.LogError("ContainerBuffsRef is not assigned!");
        if (OutlineRef == null)
            _logger.LogError("OutlineRef is not assigned!");
        if (HpStateBarRef == null)
            _logger.LogError("HpStateBarRef is not assigned!");
        if (SkillProgressBarRef == null)
            _logger.LogError("SkillProgressBarRef is not assigned!");
    }
}
