using Godot;
using System;

public partial class PanelSkillInfo : Panel
{
    ButtonSkillBase _mouseOnButtonSkill;
    public ButtonSkillBase MouseOnButtonSkill
    {
        get => _mouseOnButtonSkill;
        set
        {
            if (_mouseOnButtonSkill != value)
            {
                _mouseOnButtonSkill = value;
                UpdateSkillInfo();
            }
        }
    }

    [Export]
    Label skillNameLabel;
    [Export]
    Label skillDescriptionLabel;

    public override void _Ready()
    {
        MouseOnButtonSkill = null;
        UpdateSkillInfo();
    }


    void UpdateSkillInfo()
    {
        if (MouseOnButtonSkill != null)
        {
            Visible = true;
            skillNameLabel.Text = MouseOnButtonSkill.BindSkill.SkillName;
            skillDescriptionLabel.Text = MouseOnButtonSkill.BindSkill.SkillDescription;
        }
        else
        {
            Visible = false;
        }
    }

}
