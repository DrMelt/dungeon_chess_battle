using System;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 技能按钮：绑定一个技能与施法单位 Pawn，点击时委托给技能列表面板发起施法 RPC。
/// 冷却期间显示灰色遮罩与剩余秒数。
/// </summary>
public partial class ButtonSkillBase : Button {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<ButtonSkillBase> _logger = ServiceLocator.GetLogger<ButtonSkillBase>();

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

    /// <summary>是否已完成 Init 初始化（未初始化时隐藏，防止悬停误触）。</summary>
    public bool IsInitialized => _bindingSkill != null;

    /// <summary>绑定的技能对象。</summary>
    public UnitSkillBaseGodot BindSkill => _bindingSkill ?? throw new InvalidOperationException("BindSkill has not been initialized.");

    /// <summary>绑定技能所属的施法单位 Pawn。</summary>
    public UnitPawn BindUnit {
        get => field ?? throw new InvalidOperationException("BindUnit has not been initialized.");
        private set;
    }

    /// <summary>技能列表面板引用（用于委托释放逻辑）。</summary>
    private SkillsList? _skillsListRef;

    /// <summary>
    /// 初始化按钮与技能、施法单位及技能列表面板的绑定，并设置技能图标。
    /// </summary>
    /// <param name="bindSkill">要绑定的技能。</param>
    /// <param name="bindUnit">技能所属的施法单位 Pawn。</param>
    /// <param name="skillsListRef">技能列表面板引用。</param>
    public void Init(UnitSkillBaseGodot bindSkill, UnitPawn bindUnit, SkillsList skillsListRef) {
        _bindingSkill = bindSkill;
        BindUnit = bindUnit;
        _skillsListRef = skillsListRef;

        Icon = bindSkill.Icon;
    }

    /// <summary>节点就绪：校验导出引用并注册鼠标悬浮 UI 判定。</summary>
    public override void _Ready() {
        ValidateExports();

        // 未调用 Init 的预置按钮（如场景中残留实例）隐藏自身，避免悬停时访问未初始化技能
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

    /// <summary>每帧更新冷却 UI（灰色遮罩与剩余秒数）。数据源为服务端权威冷却。</summary>
    public override void _Process(double delta) {
        if (_bindingSkill == null)
            return;

        var unit = IsInitialized ? BindUnit : null;
        if (unit == null)
            return;

        float remaining = unit.GetTotalCooldownRemaining(_bindingSkill!.SkillId);
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
