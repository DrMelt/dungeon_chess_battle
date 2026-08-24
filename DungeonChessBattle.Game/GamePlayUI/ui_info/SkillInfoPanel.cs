using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 技能信息面板，悬停技能按钮或 Buff 图标时显示其名称与描述。
/// </summary>
public partial class SkillInfoPanel : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<SkillInfoPanel> _logger = ServiceLocator.GetLogger<SkillInfoPanel>();

    /// <summary>玩家界面资源引用，用于读取悬停控件。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceRes;

    /// <summary>Buff 资源表，用于按 BuffTypeId 匹配名称与描述。</summary>
    [Export]
    private BuffResourceTable? buffResourceTable;

    /// <summary>技能名称标签。</summary>
    [ExportGroup("Internal")]
    [Export]
    private Label? skillNameLabel;
    /// <summary>技能描述标签。</summary>
    [Export]
    private Label? skillDescriptionLabel;

    /// <summary>上一帧的悬停控件，用于变化检测。</summary>
    private Control? _lastMouseOnControl;

    /// <summary>
    /// 节点就绪：校验导出引用并初始化显示。
    /// </summary>
    public override void _Ready() {
        if (playerInterfaceRes == null)
            _logger.LogError("playerInterfaceRes is not assigned!");
        if (skillNameLabel == null)
            _logger.LogError("skillNameLabel is not assigned!");
        if (skillDescriptionLabel == null)
            _logger.LogError("skillDescriptionLabel is not assigned!");
        if (buffResourceTable == null)
            _logger.LogError("buffResourceTable is not assigned!");

        UpdateInfo(null);
    }

    /// <summary>
    /// 每帧检查悬停控件变化并刷新信息显示。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var current = playerInterfaceRes?.MouseOnUIControl;
        if (current == _lastMouseOnControl)
            return;
        _lastMouseOnControl = current;
        UpdateInfo(current);
    }

    /// <summary>
    /// 更新信息显示：悬停在技能按钮或 Buff 图标上时展示对应内容，否则隐藏。
    /// </summary>
    /// <param name="control">当前悬停的 UI 控件。</param>
    private void UpdateInfo(Control? control) {
        bool isShow = false;

        if (control != null) {
            if (control is ButtonSkillBase { IsInitialized: true } mouseOnButtonSkill) {
                skillNameLabel?.Text = mouseOnButtonSkill.BindSkill.SkillName;
                skillDescriptionLabel?.Text = mouseOnButtonSkill.BindSkill.SkillDescription;
                isShow = true;
            }
            else if (control is TextureRectBuffIcon buffIcon) {
                var buffRes = buffResourceTable?.GetResourceByBuffTypeId(buffIcon.BindingBuffData.BuffTypeId);
                skillNameLabel?.Text = buffRes?.BuffName ?? $"Buff({buffIcon.BindingBuffData.BuffTypeId})";
                skillDescriptionLabel?.Text = buffRes?.BuffDescription ?? string.Empty;
                isShow = true;
            }
        }
        Visible = isShow;
    }


}
