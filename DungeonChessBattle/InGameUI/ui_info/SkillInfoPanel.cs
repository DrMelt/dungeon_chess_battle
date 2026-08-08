using Godot;
using DungeonChessBattle.GameAssets.Buffs;

namespace DungeonChessBattle;

/// <summary>
/// 技能信息面板，悬停技能按钮或 Buff 图标时显示其名称与描述。
/// </summary>
public partial class SkillInfoPanel : Control {
    /// <summary>玩家界面资源引用，用于订阅悬停控件变化事件。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceRes;

    /// <summary>技能名称标签。</summary>
    [ExportGroup("Internal")]
    [Export]
    private Label? skillNameLabel;
    /// <summary>技能描述标签。</summary>
    [Export]
    private Label? skillDescriptionLabel;

    /// <summary>
    /// 节点就绪：订阅悬停控件变化事件并初始化显示。
    /// </summary>
    public override void _Ready() {
        if (playerInterfaceRes == null)
            GD.PrintErr("[SkillInfoPanel] [Export] playerInterfaceRes is not assigned!");
        if (skillNameLabel == null)
            GD.PrintErr("[SkillInfoPanel] [Export] skillNameLabel is not assigned!");
        if (skillDescriptionLabel == null)
            GD.PrintErr("[SkillInfoPanel] [Export] skillDescriptionLabel is not assigned!");

        playerInterfaceRes?.MouseOnUIControlChanged += UpdateInfo;
        UpdateInfo(null);
    }

    /// <summary>
    /// 节点退出场景树时取消订阅事件。
    /// </summary>
    public override void _ExitTree() {
        playerInterfaceRes?.MouseOnUIControlChanged -= UpdateInfo;
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
                var buffRes = BuffResourceTable.GetResourceByBuffTypeId(buffIcon.BindingBuffData.BuffTypeId);
                skillNameLabel?.Text = buffRes?.BuffName ?? $"Buff({buffIcon.BindingBuffData.BuffTypeId})";
                skillDescriptionLabel?.Text = buffRes?.BuffDescription ?? string.Empty;
                isShow = true;
            }
        }
        Visible = isShow;
    }


}
