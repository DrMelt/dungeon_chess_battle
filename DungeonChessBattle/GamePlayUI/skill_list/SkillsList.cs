using System.Collections.Generic;
using System.Linq;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Logic.Combat;
using DungeonChessBattle.GameAssets;
using DungeonChessBattle.GamePlayUI.skill_list;
using DungeonChessBattle.MainScene;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 技能列表面板：按技能目标类型（单位/位置/无目标）分发施法 RPC。
/// 每帧从战斗单位管理器直读本地玩家操控角色作为固定显示单位，变化时重建全部按钮。
/// 施法由服务端权威读条与结算，施法目标仍取当前选中单位。
/// </summary>
public partial class SkillsList : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<SkillsList> _logger = ServiceLocator.GetLogger<SkillsList>();

    /// <summary>节点引用容器。</summary>
    public SkillsListInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>战斗单位管理器引用，提供当前显示单位与施法服务。</summary>
    [Export]
    private BattleUnitManager? _unitManagerRef;

    /// <summary>玩家交互状态引用，用于位置目标瞄准时读取鼠标地面位置并写等待标志。</summary>
    [Export]
    public PlayerInterfaceRes? PlayerInterfaceRes {
        get; private set;
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

    /// <summary>当前展示的技能所属单位 NetId，用于变化检测。</summary>
    private ushort? _shownUnitId;

    /// <summary>技能预输入缓冲，不可施放时缓存按键并在可施放时自动施放。</summary>
    private SkillPreInput? _preInput;

    /// <summary>
    /// 节点就绪：获取引用集合节点并校验导出引用。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<SkillsListInterRefs>("SkillsListInterRefs");
        if (InterRefs == null) {
            _logger.LogError("SkillsListInterRefs node not found.");
            return;
        }
        if (_unitManagerRef == null) {
            _logger.LogError("_unitManagerRef is not assigned!");
            return;
        }
        _preInput = CreatePreInput();
    }

    /// <summary>
    /// 组装预输入缓冲：判定直接消费按钮持有的技能定义，走领域共享静态判定
    /// SkillCastValidator（目标/位置由本地场景解析），施放经当前战斗服务发送。
    /// </summary>
    private SkillPreInput? CreatePreInput() {
        var manager = _unitManagerRef;
        if (manager == null)
            return null;
        return new SkillPreInput(
            (skill, targetNetId, posX, posZ) =>
                CanCastLocal(manager, skill, targetNetId, posX, posZ),
            new BattleSkillCaster(
                () => manager.BattleService,
                () => manager.RoomId,
                () => manager.LocalUnitShow?.Pawn.Id ?? 0),
            clock: null);
    }

    /// <summary>本地位施放判定：本地方单位状态 + 解析目标后统一走 SkillCastValidator。</summary>
    private static bool CanCastLocal(BattleUnitManager manager, SkillDefinition skill,
        ushort targetNetId, float posX, float posZ) {
        var pawn = manager.LocalUnitShow?.Pawn;
        if (pawn == null)
            return false;
        if (!manager.TryGetCampRelations(out var relations))
            return false;
        var target = targetNetId != 0 ? manager.UnitsArr.FirstOrDefault(u => u.Id == targetNetId) : null;
        return SkillCastValidator.CanCast(pawn, skill, target, new System.Numerics.Vector2(posX, posZ), relations);
    }

    /// <summary>
    /// 每帧处理位置目标选择输入，并对本地玩家角色做脏检查（变化时重建按钮列表）。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        HandleWaitPosTargetInput();
        _preInput?.Refresh();

        var manager = _unitManagerRef;
        var showUnit = manager?.LocalUnitShow;
        ushort? shownId = showUnit?.Pawn.Id;
        if (shownId == _shownUnitId)
            return;
        _shownUnitId = shownId;
        UpdateSkillsList(showUnit);
    }

    /// <summary>处理等待位置目标时的确认/取消输入。</summary>
    private void HandleWaitPosTargetInput() {
        if (_state != SkillReleaseState.WaitingPosTarget || _waitingButton == null)
            return;

        if (Input.IsActionJustPressed("Skill_UnSelectTarget")) {
            CancelWait();
            return;
        }

        if (Input.IsActionJustPressed("Skill_SelectTarget")) {
            var targetPos = PlayerInterfaceRes?.MouseGroundPosition;
            if (targetPos != null) {
                var v = targetPos.Value;
                var skill = _waitingButton.BindSkill.InternalConfig;
                if (skill != null)
                    SubmitCast(skill, 0, v.X, v.Z, _waitingButton);
            }
            CancelWait();
        }
    }

    /// <summary>
    /// 根据单位更新技能按钮列表（重建全部按钮），无显示单位时清空。
    /// </summary>
    /// <param name="unitShow">目标单位的展示对象，无则清空按钮。</param>
    private void UpdateSkillsList(UnitGameShow? unitShow) {
        CancelWait();
        _preInput?.Clear();

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
        var skill = button.BindSkill.InternalConfig;
        var unitsManager = _unitManagerRef;
        if (skill == null || unitsManager == null) {
            button.ButtonPressed = false;
            return;
        }

        // NeedUnitTarget：优先使用已锁定的本地焦点单位，目标阵营需满足技能目标策略
        if (skill.NeedUnitTarget) {
            var targetUnit = unitsManager.LocalFocusUnit;
            var selfPawn = unitsManager.LocalUnitShow?.Pawn;

            // 焦点目标合法时直接施放到焦点目标
            if (targetUnit != null
                && unitsManager.TryGetCampRelations(out var relations)
                && SkillTargetValidator.CanAffect(button.BindPawn, targetUnit.Pawn, skill.TargetPolicy, relations)) {
                SubmitCast(skill, targetUnit.Pawn.Id, 0f, 0f, button);
                return;
            }

            // 无焦点目标或焦点目标不合法：允许对友方释放的技能回退为对自身施放
            if (skill.TargetPolicy.HasFlag(SkillTargetPolicy.Same) && selfPawn != null) {
                SubmitCast(skill, selfPawn.Id, 0f, 0f, button);
                return;
            }

            button.ButtonPressed = false;
            return;
        }

        // NeedPosTarget：进入等待状态，按钮保持按下
        if (skill.NeedPosTarget) {
            _state = SkillReleaseState.WaitingPosTarget;
            _waitingButton = button;
            PlayerInterfaceRes?.IsWaitingSkillTarget = true;
            return;
        }

        // 无目标需求：提交施法意图
        SubmitCast(skill, 0, 0f, 0f, button);
    }

    /// <summary>提交施法意图到预输入缓冲：可施放立即施放，否则入队并在可施放时自动施放。</summary>
    private void SubmitCast(SkillDefinition skill, ushort targetNetId, float posX, float posZ, ButtonSkillBase button) {
        _preInput?.Submit(skill, targetNetId, posX, posZ);
        button.ButtonPressed = false;
    }

    /// <summary>
    /// 取消等待位置目标状态并通知交互状态。
    /// </summary>
    private void CancelWait() {
        _waitingButton?.ButtonPressed = false;
        _waitingButton = null;
        _state = SkillReleaseState.Idle;
        PlayerInterfaceRes?.IsWaitingSkillTarget = false;
    }
}
