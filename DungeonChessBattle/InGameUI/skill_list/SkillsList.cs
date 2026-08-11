using Godot;
using System.Collections.Generic;

namespace DungeonChessBattle;

/// <summary>
/// 技能列表面板：展示单位全部技能按钮，并按技能目标类型（单位/位置/无目标）分发施法 RPC。
/// 施法由服务端权威读条与结算，客户端仅发起请求。
/// </summary>
public partial class SkillsList : Control {
    /// <summary>节点引用容器。</summary>
    public SkillsListInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>战斗单位管理器引用（用于获取可命中单位数组与施法服务）。</summary>
    public BattleUnitManager? UnitsInGameRef {
        get; set;
    }

    /// <summary>玩家操作界面资源引用（用于获取当前聚焦单位与鼠标地面位置）。</summary>
    public PlayerInterfaceRes? PlayerInterfaceRes {
        get; set;
    }

    /// <summary>ViewModel 引用（由 PlayerUIRoot 在绑定时注入），用于通知目标等待状态。</summary>
    public PlayerOperationInterfaceInfo? ViewModel {
        get; set;
    }

    /// <summary>技能释放状态机状态。</summary>
    private enum SkillReleaseState {
        /// <summary>空闲。</summary>
        Idle,
        /// <summary>等待玩家选择位置目标。</summary>
        WaitingPosTarget
    }

    /// <summary>当前技能释放状态。</summary>
    private SkillReleaseState _state = SkillReleaseState.Idle;

    /// <summary>等待位置目标时按下的技能按钮。</summary>
    private ButtonSkillBase? _waitingButton;

    /// <summary>当前面板创建的全部技能按钮列表。</summary>
    private readonly List<ButtonSkillBase> _skillButtonList = [];

    /// <summary>获取技能列表面板子节点引用。</summary>
    public override void _Ready() {
        InterRefs = GetNode<SkillsListInterRefs>("SkillsListInterRefs");
        if (InterRefs == null) {
            GD.PrintErr("[SkillsList] SkillsListInterRefs node not found.");
            return;
        }
    }

    /// <summary>
    /// 根据单位更新技能按钮列表（重建全部按钮）。
    /// </summary>
    /// <param name="unitShow">目标单位的展示对象。</param>
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
            buttonSkill.Init(skill, unitShow.Pawn, this);
            hBox.AddChild(buttonSkill);
            _skillButtonList.Add(buttonSkill);
        }
    }

    /// <summary>
    /// 技能按钮点击处理：按技能目标类型分发（单位目标 / 位置目标 / 无目标），并发起施法 RPC。
    /// </summary>
    /// <param name="button">被点击的技能按钮。</param>
    public void OnSkillButtonPressed(ButtonSkillBase button) {
        var skill = button.BindSkill;
        var unitsManager = UnitsInGameRef;
        var service = unitsManager?.BattleService;
        if (service == null) {
            button.ButtonPressed = false;
            return;
        }
        var roomId = unitsManager?.RoomId ?? "";
        var casterName = button.BindPawn.UnitName.Value;

        // NeedUnitTarget：使用已锁定的 FocusOnUnit，同步发起施法 RPC
        if (skill.NeedUnitTarget) {
            var targetUnit = PlayerInterfaceRes?.FocusOnUnit;
            if (targetUnit == null) {
                button.ButtonPressed = false;
                return;
            }
            service.CastSkill(roomId, casterName, targetUnit.Pawn.UnitName.Value, skill.SkillId);
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

        // 无目标需求：同步发起施法 RPC
        service.CastSkill(roomId, casterName, null, skill.SkillId);
        button.ButtonPressed = false;
    }


    /// <summary>每帧处理位置目标选择输入（确认/取消）。</summary>
    public override void _Process(double delta) {
        if (_state != SkillReleaseState.WaitingPosTarget || _waitingButton == null)
            return;

        if (Input.IsActionJustPressed("Skill_UnSelectTarget")) {
            CancelWait();
            return;
        }

        if (Input.IsActionJustPressed("Skill_SelectTarget")) {
            var targetPos = PlayerInterfaceRes?.MouseGoundPosition;
            if (targetPos != null) {
                var v = targetPos.Value;
                var skill = _waitingButton.BindSkill;
                var unitsManager = UnitsInGameRef;
                var service = unitsManager?.BattleService;
                service?.CastSkill(
                        unitsManager?.RoomId ?? "",
                        _waitingButton.BindPawn.UnitName.Value,
                        null,
                        skill.SkillId,
                        v.X,
                        v.Z);
            }
            CancelWait();
        }
    }

    /// <summary>
    /// 取消等待位置目标状态并通知 ViewModel。
    /// </summary>
    private void CancelWait() {
        _waitingButton?.ButtonPressed = false;
        _waitingButton = null;
        _state = SkillReleaseState.Idle;
        ViewModel?.NotifyWaitingSkillTarget(false);
    }

    /// <summary>是否处于等待目标选择状态。</summary>
    public bool IsWaitTarget() {
        return _state != SkillReleaseState.Idle;
    }

    /// <summary>获取正在等待位置目标的技能按钮列表。</summary>
    public List<ButtonSkillBase> WaitingTargetSkillList() {
        if (_waitingButton != null)
            return [_waitingButton];
        return [];
    }
}
