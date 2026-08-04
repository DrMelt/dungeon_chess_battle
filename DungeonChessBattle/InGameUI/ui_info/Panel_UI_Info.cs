using Godot;

namespace DungeonChessBattle;

public partial class Panel_UI_Info : Control {
    [Export]
    UserInterfaceRes? userInterfaceRes;

    [ExportGroup("Internal")]
    [Export]
    Label? skillNameLabel;
    [Export]
    Label? skillDescriptionLabel;

    public override void _Ready() {
        if (userInterfaceRes == null)
            GD.PrintErr("[Panel_UI_Info] [Export] userInterfaceRes is not assigned!");
        if (skillNameLabel == null)
            GD.PrintErr("[Panel_UI_Info] [Export] skillNameLabel is not assigned!");
        if (skillDescriptionLabel == null)
            GD.PrintErr("[Panel_UI_Info] [Export] skillDescriptionLabel is not assigned!");

        userInterfaceRes?.MouseOnUIControlChanged += UpdateInfo;
        UpdateInfo(null);
    }
    public override void _ExitTree() {
        userInterfaceRes?.MouseOnUIControlChanged -= UpdateInfo;
    }

    private void UpdateInfo(Control? control) {
        bool isShow = false;

        if (control != null) {
            if (control is ButtonSkillBase mouseOnButtonSkill) {
                skillNameLabel?.Text = mouseOnButtonSkill.BindSkill.SkillName;
                skillDescriptionLabel?.Text = mouseOnButtonSkill.BindSkill.SkillDescription;
                isShow = true;
            }
            else if (control is TextureRectBuffIcon buffIcon) {
                skillNameLabel?.Text = buffIcon.BindingBuff.BuffName;
                skillDescriptionLabel?.Text = buffIcon.BindingBuff.BuffDescription;
                isShow = true;
            }
        }
        Visible = isShow;
    }


}
