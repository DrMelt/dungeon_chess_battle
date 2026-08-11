using System;
using DungeonChessBattle.Entities;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 技能按钮：绑定一个技能与施法单位 Pawn，点击时委托给技能列表面板发起施法 RPC。
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

    /// <summary>是否已完成 Init 初始化（未初始化时隐藏，防止悬停误触）。</summary>
    public bool IsInitialized => _bindingSkill != null;

    /// <summary>绑定的技能对象。</summary>
    public UnitSkillBaseGodot BindSkill => _bindingSkill ?? throw new InvalidOperationException("BindSkill has not been initialized.");

    /// <summary>绑定技能所属的施法单位 Pawn。</summary>
    public UnitPawn BindPawn {
        get => field ?? throw new InvalidOperationException("BindPawn has not been initialized.");
        private set;
    }

    /// <summary>技能列表面板引用（用于委托释放逻辑）。</summary>
    private SkillsList? _skillsListRef;

    /// <summary>
    /// 初始化按钮与技能、施法单位 Pawn 及技能列表面板的绑定，并设置技能图标。
    /// </summary>
    /// <param name="bindSkill">要绑定的技能。</param>
    /// <param name="bindPawn">技能所属的施法单位 Pawn。</param>
    /// <param name="skillsListRef">技能列表面板引用。</param>
    public void Init(UnitSkillBaseGodot bindSkill, UnitPawn bindPawn, SkillsList skillsListRef) {
        _bindingSkill = bindSkill;
        BindPawn = bindPawn;
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
            GD.PrintErr("[ButtonSkillBase] [Export] _playerInterfaceRes is not assigned!");
        if (_labelCooldownTimeRef == null)
            GD.PrintErr("[ButtonSkillBase] [Export] _labelCooldownTimeRef is not assigned!");
    }

    /// <summary>点击按钮时委托给技能列表面板的全局状态机处理释放逻辑。</summary>
    public override void _Pressed() {
        _skillsListRef?.OnSkillButtonPressed(this);
    }

    /// <summary>每帧更新冷却 UI（灰色遮罩与剩余秒数）。数据源为 Pawn 服务端权威冷却。</summary>
    public override void _Process(double delta) {
        if (_bindingSkill == null)
            return;

        var pawn = IsInitialized ? BindPawn : null;
        if (pawn == null)
            return;

        float remaining = GetSkillCooldownRemaining(pawn);
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

    /// <summary>
    /// 计算该技能按钮当前的冷却剩余秒数（GCD 与个体技能冷却取较大者）。
    /// </summary>
    /// <param name="pawn">施法单位 Pawn。</param>
    /// <returns>剩余冷却秒数；无冷却返回 0。</returns>
    private float GetSkillCooldownRemaining(UnitPawn pawn) {
        float remaining = pawn.GcdRemaining.Value;
        foreach (var cd in pawn.SkillCooldowns) {
            if (cd.SkillId == _bindingSkill!.SkillId && cd.Remaining > remaining)
                remaining = cd.Remaining;
        }
        return remaining;
    }
}
