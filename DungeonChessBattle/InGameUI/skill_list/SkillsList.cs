using Godot;
using System.Collections.Generic;

namespace DungeonChessBattle;

public partial class SkillsList : Control {
    // ── InterRefs ──
    public SkillsListInterRefs? InterRefs {
        get; private set;
    }

    // ── 运行时注入的外部引用 ──
    public UnitsInScene_Show? UnitsInGameRef {
        get; set;
    }
    public UserInterfaceRes? UserInterfaceRes {
        get; set;
    }
    /// <summary>ViewModel 引用（由 Node2d_UserUI 在绑定时注入），用于通知目标等待状态</summary>
    public UserOperationInterfaceInfo? ViewModel {
        get; set;
    }

    // ── 技能释放状态机 ──
    private enum SkillReleaseState {
        Idle, WaitingPosTarget
    }
    private SkillReleaseState _state = SkillReleaseState.Idle;
    private ButtonSkillBase? _waitingButton;

    private readonly List<ButtonSkillBase> _skillButtonList = [];

    public override void _Ready() {
        InterRefs = GetNode<SkillsListInterRefs>("SkillsListInterRefs");
        if (InterRefs == null) {
            GD.PrintErr("[SkillsList] SkillsListInterRefs node not found.");
            return;
        }
    }

    internal void UpdateSkillsList(UnitGameShow unitShow) {
        CancelWait();

        var hBox = InterRefs?.HBoxContainerRef;
        if (hBox == null)
            return;

        var children = hBox.GetChildren();
        foreach (var child in children) {
            child.QueueFree();
        }
        _skillButtonList.Clear();

        var packedScene = InterRefs?.SkillButtonPackedScene;
        if (unitShow?.SkillsList == null || packedScene == null)
            return;

        foreach (UnitSkillBaseGodot skill in unitShow.SkillsList) {
            var buttonSkill = packedScene.Instantiate<ButtonSkillBase>();
            buttonSkill.Init(skill, unitShow.UnitStateRec, this);
            hBox.AddChild(buttonSkill);
            _skillButtonList.Add(buttonSkill);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 技能释放入口 — 由 ButtonSkillBase._Pressed() 委托调用
    // ═══════════════════════════════════════════════════════════

    public void OnSkillButtonPressed(ButtonSkillBase button) {
        var skill = button.BindSkill;
        var unitsArr = UnitsInGameRef?.UnitsArr;

        // NeedUnitTarget：使用已锁定的 FocusOnUnit，同步释放
        if (skill.NeedUnitTarget) {
            var targetUnit = UserInterfaceRes?.FocusOnUnit;
            if (targetUnit == null) {
                button.ButtonPressed = false;
                return;
            }
            skill.SetSkill(
                button.BindUnitState,
                targetUnit.UnitStateRec,
                null,
                unitsArr ?? []);
            button.ButtonPressed = false;
            return;
        }

        // NeedPosTarget：进入等待状态，按钮保持按下
        if (skill.NeedPosTarget) {
            _state = SkillReleaseState.WaitingPosTarget;
            _waitingButton = button;
            ViewModel?.NotifyWaitingSkillTarget(true);
            return;
        }

        // 无目标需求：同步释放
        skill.SetSkill(button.BindUnitState, null!, null, unitsArr ?? []);
        button.ButtonPressed = false;
    }

    // ═══════════════════════════════════════════════════════════
    // 统一 Input 轮询 — 仅在等待位置目标时生效
    // ═══════════════════════════════════════════════════════════

    public override void _Process(double delta) {
        if (_state != SkillReleaseState.WaitingPosTarget || _waitingButton == null)
            return;

        if (Input.IsActionJustPressed("Skill_UnSelectTarget")) {
            CancelWait();
            return;
        }

        if (Input.IsActionJustPressed("Skill_SelectTarget")) {
            var targetPos = UserInterfaceRes?.MouseGoundPosition;
            if (targetPos != null) {
                var v = targetPos.Value;
                var skill = _waitingButton.BindSkill;
                skill.SetSkill(
                    _waitingButton.BindUnitState,
                    null!,
                    new System.Numerics.Vector3(v.X, v.Y, v.Z),
                    UnitsInGameRef?.UnitsArr ?? []);
            }
            CancelWait();
        }
    }

    private void CancelWait() {
        _waitingButton?.ButtonPressed = false;
        _waitingButton = null;
        _state = SkillReleaseState.Idle;
        ViewModel?.NotifyWaitingSkillTarget(false);
    }

    // ═══════════════════════════════════════════════════════════
    // 查询接口 — 供 Node2d_UserUI 等外部消费者使用
    // ═══════════════════════════════════════════════════════════

    public bool IsWaitTarget() {
        return _state != SkillReleaseState.Idle;
    }

    public List<ButtonSkillBase> WaitingTargetSkillList() {
        if (_waitingButton != null)
            return [_waitingButton];
        return [];
    }
}
