using Godot;

namespace DungeonChessBattle;

public partial class Panel_UI_Info : Control {
    [Export]
    UserInterfaceRes userInterfaceRes = null!;

    [ExportGroup("Internal")]
    [Export]
    Label skillNameLabel = null!;
    [Export]
    Label skillDescriptionLabel = null!;

    public override void _Ready() {
        userInterfaceRes.MouseOnUIControlChangedEvent += UpdateInfo;
        UpdateInfo(null!);
    }
    public override void _ExitTree() {
        userInterfaceRes.MouseOnUIControlChangedEvent -= UpdateInfo;
    }

    private void UpdateInfo(Control control) {
        bool isShow = false;

        if (control != null) {
            if (control is ButtonSkillBase mouseOnButtonSkill) {
                skillNameLabel.Text = mouseOnButtonSkill.BindSkill.SkillName;
                skillDescriptionLabel.Text = mouseOnButtonSkill.BindSkill.SkillDescription;
                isShow = true;
            }
            else if (control is TextureRectBuffIcon buffIcon) {
                skillNameLabel.Text = buffIcon.BindingBuff.buffName;
                skillDescriptionLabel.Text = buffIcon.BindingBuff.buffDescription;
                isShow = true;
            }
        }
        Visible = isShow;
    }


}
