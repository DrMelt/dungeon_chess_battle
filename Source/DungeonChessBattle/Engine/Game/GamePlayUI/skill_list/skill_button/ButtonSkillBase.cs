using System;
using DungeonChessBattle.Battle.Config.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.Game.Shared.Display;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 技能按钮：绑定一个技能定义与其展示数据、施法单位，点击时委托给技能列表面板发起施法 RPC。
/// 冷却期间显示灰色遮罩与剩余秒数。
/// </summary>
public partial class ButtonSkillBase : Button {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ButtonSkillBase> _logger = ServiceLocator.GetLogger<ButtonSkillBase>();

    /// <summary>玩家操作界面资源引用，用于鼠标悬浮 UI 判定。</summary>
    [Export]
    private PlayerInterfaceRes? _playerInterfaceRes;

    /// <summary>冷却期间按钮的调制色。</summary>
    [Export]
    private Color _coolingColor = new(0.5f, 0.5f, 0.5f, 1.0f);

    /// <summary>冷却时间文本标签引用，可空。</summary>
    [Export]
    private Label? _labelCooldownTimeRef;

    /// <summary>绑定的技能定义，由 Init 注入。</summary>
    private SkillDefinition? _bindingSkill;

    /// <summary>绑定的技能展示数据，mod 未声明该技能展示时为 null。</summary>
    private SkillDisplay? _bindingDisplay;

    /// <summary>是否已完成 Init 初始化；未初始化时隐藏。</summary>
    public bool IsInitialized => _bindingSkill != null;

    /// <summary>绑定的技能定义，施法规则与冷却匹配读它。</summary>
    public SkillDefinition BindSkill => _bindingSkill ?? throw new InvalidOperationException("BindSkill has not been initialized.");

    /// <summary>绑定的技能展示数据，图标/名称/描述/范围提示场景读它；未声明展示时为 null。</summary>
    public SkillDisplay? BindDisplay => _bindingDisplay;

    /// <summary>绑定技能所属的单位只读视图。</summary>
    public IBattleUnitView BindUnit {
        get => field ?? throw new InvalidOperationException("BindUnit has not been initialized.");
        private set;
    }

    /// <summary>技能列表面板引用，用于委托释放逻辑。</summary>
    private SkillsList? _skillsListRef;

    /// <summary>
    /// 初始化按钮与技能定义、展示数据、施法单位及技能列表面板的绑定，并按展示数据设置图标。
    /// </summary>
    /// <param name="bindSkill">要绑定的技能定义。</param>
    /// <param name="bindDisplay">该技能的展示数据，未声明时为 null。</param>
    /// <param name="bindUnit">技能所属的单位只读视图。</param>
    /// <param name="skillsListRef">技能列表面板引用。</param>
    public void Init(
        SkillDefinition bindSkill, SkillDisplay? bindDisplay,
        IBattleUnitView bindUnit, SkillsList skillsListRef) {
        _bindingSkill = bindSkill;
        _bindingDisplay = bindDisplay;
        BindUnit = bindUnit;
        _skillsListRef = skillsListRef;

        // 未声明图标时保留场景配置的占位图标
        if (bindDisplay?.Icon is { } icon)
            Icon = icon;
    }

    /// <summary>节点就绪：校验导出引用并注册鼠标悬浮 UI 判定。</summary>
    public override void _Ready() {
        ValidateExports();

        // 未调用 Init 的按钮隐藏自身，避免悬停时访问未初始化技能
        if (!IsInitialized) {
            Visible = false;
            MouseDefaultCursorShape = CursorShape.Arrow;
            return;
        }

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
            _logger.LogError("_playerInterfaceRes is not assigned!");
        if (_labelCooldownTimeRef == null)
            _logger.LogError("_labelCooldownTimeRef is not assigned!");
    }

    /// <summary>点击按钮时委托给技能列表面板的全局状态机处理释放逻辑。</summary>
    public override void _Pressed() {
        _skillsListRef?.OnSkillButtonPressed(this);
    }

    /// <summary>每帧更新冷却 UI：灰色遮罩与剩余秒数，数据源为服务端权威冷却。</summary>
    public override void _Process(double delta) {
        if (_bindingSkill == null)
            return;

        var unit = IsInitialized ? BindUnit : null;
        if (unit == null || _bindingSkill == null)
            return;

        float remaining = unit.GetTotalCooldownRemaining(_bindingSkill.SkillId);
        if (remaining > 0f) {
            SelfModulate = _coolingColor;
            if (_labelCooldownTimeRef != null) {
                _labelCooldownTimeRef.Visible = true;
                _labelCooldownTimeRef.Text = remaining.ToString("F1");
            }
        }
        else {
            SelfModulate = new Color(1, 1, 1, 1);
            _labelCooldownTimeRef?.Visible = false;
        }
    }

}
