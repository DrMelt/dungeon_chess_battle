using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// StateBar2d_Focus 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBar2d_FocusInterRefs : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<StateBar2d_FocusInterRefs> _logger = ServiceLocator.GetLogger<StateBar2d_FocusInterRefs>();

    /// <summary>玩家 UI 设置资源。</summary>
    [Export]
    public PlayerUISettings? PlayerUISettingsRef {
        get; set;
    }

    /// <summary>Buff 图标容器组件。</summary>
    [Export]
    public ContainerBuffs? HboxContainerBuffsRef {
        get; set;
    }

    /// <summary>血条状态组件。</summary>
    [Export]
    public HP_StateBar? PanelFocusStateRef {
        get; set;
    }

    /// <summary>施法进度条组件。</summary>
    [Export]
    public SkillProgressBar? PanelSkillProgressBarRef {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (PlayerUISettingsRef == null)
            _logger.LogError("PlayerUISettingsRef is not assigned!");
        if (HboxContainerBuffsRef == null)
            _logger.LogError("HboxContainerBuffsRef is not assigned!");
        if (PanelFocusStateRef == null)
            _logger.LogError("PanelFocusStateRef is not assigned!");
        if (PanelSkillProgressBarRef == null)
            _logger.LogError("PanelSkillProgressBarRef is not assigned!");
    }
}
