using System;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 技能按钮：绑定一个技能并在点击时委托给技能列表面板处理释放。
/// 冷却期间显示灰色遮罩与剩余秒数。
/// </summary>
public partial class ButtonSkillBase : Button {
    /// <summary>玩家操作界面资源引用（用于鼠标悬浮 UI 判定）。</summary>
    [Export]
    private PlayerInterfaceRes? _playerInterfaceRes;

    /// <summary>冷却期间按钮的调制色。</summary>
    [Export]
    private Color _coolingColor = new(0.5f, 0.5f, 0.5f, 1.0f);

    /// <summary>冷却时间文本标签引用（可选）。</summary>
    [Export]
    private Label? _labelCooldownTimeRef;

    /// <summary>绑定的技能（由 Init 注入）。</summary>
    private UnitSkillBaseGodot? _bindingSkill;

    /// <summary>绑定的技能对象。</summary>
    public UnitSkillBaseGodot BindSkill => _bindingSkill ?? throw new InvalidOperationException("BindSkill has not been initialized.");

    /// <summary>绑定技能所属的单位。</summary>
    public UnitState BindUnitState {
        get => field ?? throw new InvalidOperationException("BindUnitState has not been initialized.");
        private set;
    }

    /// <summary>技能列表面板引用（用于委托释放逻辑）。</summary>
    private SkillsList? _skillsListRef;

    /// <summary>
    /// 初始化按钮与技能、单位及技能列表面板的绑定，并设置技能图标。
    /// </summary>
    /// <param name="bindSkill">要绑定的技能。</param>
    /// <param name="bindUnitState">技能所属的单位。</param>
    /// <param name="skillsListRef">技能列表面板引用。</param>
    public void Init(UnitSkillBaseGodot bindSkill, UnitState bindUnitState, SkillsList skillsListRef) {
        _bindingSkill = bindSkill;
        BindUnitState = bindUnitState;
        _skillsListRef = skillsListRef;

        Icon = bindSkill.Icon;
    }

    /// <summary>初始化按钮：校验导出并注册鼠标悬浮 UI 判定。</summary>
    public override void _Ready() {
        ValidateExports();

        var uiRes = _playerInterfaceRes;
        MouseEntered += () => {
            uiRes?.MouseOnUIControl = this;
        };

        MouseExited += () => {
            if (uiRes != null && uiRes.MouseOnUIControl == this)
                uiRes.MouseOnUIControl = null;
        };
    }

    private void ValidateExports() {
        if (_playerInterfaceRes == null)
            GD.PrintErr("[ButtonSkillBase] [Export] _playerInterfaceRes is not assigned!");
        if (_labelCooldownTimeRef == null)
            GD.PrintErr("[ButtonSkillBase] [Export] _labelCooldownTimeRef is not assigned!");
    }

    /// <summary>点击按钮时委托给技能列表面板的全局状态机处理释放逻辑。</summary>
    public override void _Pressed() {
        _skillsListRef?.OnSkillButtonPressed(this);
    }

    /// <summary>每帧更新冷却 UI（灰色遮罩与剩余秒数）。</summary>
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
