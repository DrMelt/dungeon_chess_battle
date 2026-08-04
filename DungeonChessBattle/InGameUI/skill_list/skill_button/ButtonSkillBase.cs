using Godot;

namespace DungeonChessBattle;

public partial class ButtonSkillBase : Button {
    [Export]
    private UserInterfaceRes? _userInterfaceRes;
    [Export]
    private Color _coolingColor = new(0.5f, 0.5f, 0.5f, 1.0f);
    [Export]
    private Label? _labelCooldownTimeRef;

    private UnitSkillBaseGodot? _bindingSkill;
    public UnitSkillBaseGodot BindSkill => _bindingSkill!;
    private UnitState? _bindUnitState;
    public UnitState BindUnitState => _bindUnitState!;

    private SkillsList? _skillsListRef;


    public void Init(UnitSkillBaseGodot bindSkill, UnitState bindUnitState, SkillsList skillsListRef) {
        _bindingSkill = bindSkill;
        _bindUnitState = bindUnitState;
        _skillsListRef = skillsListRef;

        Icon = bindSkill.Icon;
    }

    public override void _Ready() {
        ValidateExports();

        var uiRes = _userInterfaceRes;
        MouseEntered += () => {
            uiRes?.MouseOnUIControl = this;
        };

        MouseExited += () => {
            if (uiRes != null && uiRes.MouseOnUIControl == this)
                uiRes.MouseOnUIControl = null!;
        };
    }

    private void ValidateExports() {
        if (_userInterfaceRes == null)
            GD.PrintErr("[ButtonSkillBase] [Export] _userInterfaceRes is not assigned!");
        if (_labelCooldownTimeRef == null)
            GD.PrintErr("[ButtonSkillBase] [Export] _labelCooldownTimeRef is not assigned!");
    }

    // ── 委托给 SkillsList 全局状态机处理释放逻辑 ──
    public override void _Pressed() {
        _skillsListRef?.OnSkillButtonPressed(this);
    }

    // ── 仅更新冷却 UI ──
    public override void _Process(double delta) {
        if (_bindingSkill == null)
            return;

        if (_bindingSkill.IsCoolingdown()) {
            SelfModulate = _coolingColor;
            if (_labelCooldownTimeRef != null) {
                _labelCooldownTimeRef.Visible = true;
                _labelCooldownTimeRef.Text = _bindingSkill.SkillCoolingTime.ToString("F1");
            }
        }
        else {
            SelfModulate = new Color(1, 1, 1, 1);
            _labelCooldownTimeRef?.Visible = false;
        }
    }
}
