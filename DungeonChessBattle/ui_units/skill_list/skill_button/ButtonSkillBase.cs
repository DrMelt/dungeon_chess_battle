using Godot;

namespace DungeonChessBattle;

public partial class ButtonSkillBase : Button {
    [Export]
    UserInterfaceRes userInterfaceRes;
    [Export]
    Color coolingColor = new(0.5f, 0.5f, 0.5f, 1.0f);
    [Export]
    Label label_CooldownTime_Ref;

    DungeonChessBattle.Core.UnitSkillBaseGodot bindingSkill;
    public DungeonChessBattle.Core.UnitSkillBaseGodot BindSkill => bindingSkill;
    DungeonChessBattle.Core.UnitState bindUnitState;
    public DungeonChessBattle.Core.UnitState BindUnitState => bindUnitState;

    SkillsList skillsListRef;


    public void Init(DungeonChessBattle.Core.UnitSkillBaseGodot bindSkill, DungeonChessBattle.Core.UnitState bindUnitState, SkillsList skillsListRef) {
        this.bindingSkill = bindSkill;
        this.bindUnitState = bindUnitState;
        this.skillsListRef = skillsListRef;

        Icon = bindSkill.Icon;
    }

    public override void _Ready() {
        MouseEntered += () => {
            userInterfaceRes.MouseOnUIControl = this;
        };

        MouseExited += () => {
            if (userInterfaceRes.MouseOnUIControl == this) {
                userInterfaceRes.MouseOnUIControl = null;
            }
        };
    }

    public bool WaitTarget => IsPressed();
    public override void _Pressed() {
        if (bindingSkill.NeedPosTarget || bindingSkill.NeedUnitTarget) {

        }
        else if (bindingSkill.NeedUnitTarget == false) {
            bindingSkill.SetSkill(bindUnitState, null, null, skillsListRef.UnitsInGameRef.UnitsArr);
            ButtonPressed = false;
        }
    }


    public override void _Process(double delta) {
        if (bindingSkill.IsCoolingdown()) {
            SelfModulate = coolingColor;
            label_CooldownTime_Ref.Visible = true;
            label_CooldownTime_Ref.Text = bindingSkill.SkillCoolingTime.ToString("F1");
        }
        else {
            SelfModulate = new Color(1, 1, 1, 1);
            label_CooldownTime_Ref.Visible = false;
        }

        if (Input.IsActionJustPressed("Skill_UnSelectTarget")) {
            ButtonPressed = false;
        }

        if (WaitTarget) {
            if (Input.IsActionJustPressed("Skill_SelectTarget")) {
                if (bindingSkill.NeedUnitTarget) {
                    UnitGameShow mouseOnUnit = userInterfaceRes.MouseOnUnit;
                    if (mouseOnUnit != null) {
                        bindingSkill.SetSkill(bindUnitState, mouseOnUnit.UnitStateRec, null, [.. skillsListRef.UnitsInGameRef.UnitsArr ?? []]);
                    }
                }
                else if (bindingSkill.NeedPosTarget) {
                    Godot.Vector3? mouseGoundPos = userInterfaceRes.MouseGoundPosition;
                    if (mouseGoundPos != null) {
                        var v = mouseGoundPos.Value;
                        bindingSkill.SetSkill(bindUnitState, null, new System.Numerics.Vector3(v.X, v.Y, v.Z), skillsListRef.UnitsInGameRef.UnitsArr);
                    }

                }
                ButtonPressed = false;
            }
        }


    }




}
