using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Range;
using DungeonChessBattle.Battle.Logic.Combat;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.GamePanels;
using DungeonChessBattle.Game.GamePlayUI.skill_list;
using DungeonChessBattle.Game.BattleScene;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.Effects;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 技能列表面板：按技能目标类型（单位/位置/无目标）分发施法 RPC。
/// 每帧从战斗会话上下文直读本地玩家操控角色作为固定显示单位，变化时重建全部按钮。
/// 本面板只做目标选择，不判可否施放：按键即经战斗会话上下文上行，服务端权威裁定可否施放、
/// 预输入排队与结算。
/// </summary>
public partial class SkillsList : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<SkillsList> _logger = ServiceLocator.GetLogger<SkillsList>();

    /// <summary>节点引用容器。</summary>
    public SkillsListInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>战斗会话上下文引用，提供施法服务、阵营判定与当前显示单位。</summary>
    [Export]
    private BattleSessionContext? _sessionRef;

    /// <summary>玩家交互状态引用，用于位置目标瞄准时读取鼠标地面位置并写等待标志。</summary>
    [Export]
    public PlayerInterfaceRes? PlayerInterfaceRes {
        get; private set;
    }

    /// <summary>范围提示协调器引用，选位置目标时按技能资源显示范围预览。</summary>
    [Export]
    private EffectHints? _effectHints;

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

    /// <summary>当前范围提示实例，随鼠标位置每帧刷新。</summary>
    private SkillRangeRect_Hint? _rangeHint;

    /// <summary>当前范围提示对应的领域范围形状参数。</summary>
    private RectShape? _rangeRect;

    /// <summary>当前面板创建的全部技能按钮列表。</summary>
    private readonly List<ButtonSkillBase> _skillButtonList = [];

    /// <summary>当前展示的技能所属单位网络 ID，用于变化检测。</summary>
    private ushort? _shownNetId;

    /// <summary>
    /// 节点就绪：获取引用集合节点并校验导出引用。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<SkillsListInterRefs>("SkillsListInterRefs");
        if (InterRefs == null) {
            _logger.LogError("SkillsListInterRefs node not found.");
            return;
        }
        if (_sessionRef == null) {
            _logger.LogError("_sessionRef is not assigned!");
            return;
        }
        if (_effectHints == null)
            _logger.LogError("_effectHints is not assigned!");
    }

    /// <summary>
    /// 每帧处理位置目标选择输入，并对本地玩家角色做脏检查（变化时重建按钮列表）。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        HandleWaitPosTargetInput();
        UpdateRangePreview();

        var showUnit = _sessionRef?.LocalUnit;
        ushort? shownNetId = showUnit?.UnitId;
        if (shownNetId == _shownNetId)
            return;
        _shownNetId = shownNetId;
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
    /// 按本地单位展示视图重建技能按钮列表，无显示单位时清空。
    /// 技能展示资源经 UnitCatalog 配置与 SkillResourceTable 装配，不依赖视图层。
    /// </summary>
    /// <param name="unit">本地单位展示视图，无则清空按钮。</param>
    private void UpdateSkillsList(IUnitUiView? unit) {
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
        if (unit == null || packedScene == null)
            return;

        var config = UnitCatalog.GetByKey(unit.UnitName);
        if (config == null)
            return;

        foreach (var skillDefinition in config.Skills) {
            var skill = ResourceTables.Skills.LoadResource(skillDefinition);
            var buttonSkill = packedScene.Instantiate<ButtonSkillBase>();
            buttonSkill.Init(skill, unit, this);
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
        var session = _sessionRef;
        if (skill == null || session == null) {
            button.ButtonPressed = false;
            return;
        }

        // NeedUnitTarget：优先使用已锁定的本地焦点单位，目标阵营需满足技能目标策略
        if (skill.NeedUnitTarget) {
            var focusUnit = session.LocalFocus;
            var selfUnit = session.LocalUnit;

            // 焦点目标合法时直接施放到焦点目标
            if (focusUnit != null
                && session.TryGetCampRelations(out var relations)
                && SkillTargetValidator.CanAffect(button.BindUnit, focusUnit, skill.TargetPolicy, relations)) {
                SubmitCast(skill, focusUnit.UnitId, 0f, 0f, button);
                return;
            }

            // 无焦点目标或焦点目标不合法：允许对友方释放的技能回退为对自身施放
            if (skill.TargetPolicy.HasFlag(SkillTargetPolicy.Same) && selfUnit != null) {
                SubmitCast(skill, selfUnit.UnitId, 0f, 0f, button);
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
            ShowRangePreview(button.BindSkill);
            return;
        }

        // 无目标需求：提交施法意图
        SubmitCast(skill, 0, 0f, 0f, button);
    }

    /// <summary>提交施法意图：按键即上行，能否施放与预输入排队全由权威裁定。</summary>
    private void SubmitCast(SkillDefinition skill, ushort targetNetId, float posX, float posZ, ButtonSkillBase button) {
        _sessionRef?.Command?.Cast(skill.SkillId, targetNetId, posX, posZ);
        button.ButtonPressed = false;
    }

    /// <summary>
    /// 取消等待位置目标状态并通知交互状态，同时销毁范围提示。
    /// </summary>
    private void CancelWait() {
        _waitingButton?.ButtonPressed = false;
        _waitingButton = null;
        _state = SkillReleaseState.Idle;
        PlayerInterfaceRes?.IsWaitingSkillTarget = false;
        _effectHints?.HideRangeHint();
        _rangeHint = null;
        _rangeRect = null;
    }

    /// <summary>
    /// 展示范围提示：按技能资源的范围提示场景创建预览实例，参数取自领域范围形状。
    /// 初始化延迟到挂载后一帧，经 UpdateRangePreview 执行，保证提示场景 _Ready 已完成。
    /// </summary>
    private void ShowRangePreview(UnitSkillBaseGodot skillRes) {
        if (_effectHints == null)
            return;
        if (skillRes.InternalConfig?.CastArea is not RectShape shape)
            return;

        var hint = _effectHints.ShowRangeHint<SkillRangeRect_Hint>(
            skillRes, _ => UpdateRangePreview());
        if (hint == null)
            return;

        _rangeHint = hint;
        _rangeRect = shape;
        UpdateRangePreview();
    }

    /// <summary>
    /// 按施法单位位置与鼠标地面位置刷新范围提示的位置与朝向。
    /// 提示作用场景尚未 _Ready 时跳过本帧，等待挂载帧后执行。
    /// </summary>
    private void UpdateRangePreview() {
        var hint = _rangeHint;
        var shape = _rangeRect;
        if (hint == null || shape == null || hint.InterRefs == null)
            return;

        var unit = _sessionRef?.LocalUnit;
        var mouse = PlayerInterfaceRes?.MouseGroundPosition;
        if (unit == null || mouse == null)
            return;

        var from = new Vector3(unit.Position.X, 0f, unit.Position.Y);
        var to = new Vector3(mouse.Value.X, 0f, mouse.Value.Z);
        hint.Init(from, to, shape.NearClamp, shape.FarClamp, shape.FromLeft, shape.ToRight);
    }
}
