using DungeonChessBattle.GameAssets;
using DungeonChessBattle.MainScene;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 战斗 UI 根容器（View 层入口）。
/// 负责管理鼠标悬停状态、转发单位焦点变化到技能列表，
/// 并接收 MainScene 直接调用的战斗绑定/解绑以初始化子组件。
/// </summary>
public partial class BattleUIRoot : Control {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleUIRoot> _logger = ServiceLocator.GetLogger<BattleUIRoot>();

    #region Exports

    /// <summary>共享交互状态引用。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceRes;

    /// <summary>战斗单位管理器引用（持有场景单位集合资源）。</summary>
    [Export]
    private BattleUnitManager? unitsInSceneRef;

    /// <summary>技能列表面板引用。</summary>
    [Export]
    private SkillsList? skillsListRef;

    /// <summary>状态变化信息面板引用。</summary>
    [Export]
    private StateChangeInfo? stateChangeInfoRef;

    /// <summary>状态条列表面板引用。</summary>
    [Export]
    private StateBarList? stateBarListRef;

    #endregion

    /// <summary>鼠标是否悬停在 UI 上。</summary>
    public bool IsMouseOn { get; private set; } = false;

    /// <summary>
    /// 节点就绪：监听鼠标悬停状态并订阅焦点单位变化事件。
    /// </summary>
    public override void _Ready() {
        MouseEntered += () => {
            IsMouseOn = true;
        };
        MouseExited += () => {
            IsMouseOn = false;
        };

        if (playerInterfaceRes == null) {
            _logger.LogError("playerInterfaceRes is not assigned!");
            return;
        }
        playerInterfaceRes.FocusOnUnitChanged += UpdateSkillList;
        if (playerInterfaceRes.FocusOnUnit != null)
            UpdateSkillList(playerInterfaceRes.FocusOnUnit);
    }

    #region Battle Bind/Unbind（由 MainScene 直接调用）

    /// <summary>
    /// 进入战斗：将单位数据源注入到子组件并初始化技能列表。
    /// </summary>
    public void BindToBattle() {
        if (unitsInSceneRef != null) {
            stateChangeInfoRef?.BindUnitsInScene(unitsInSceneRef.UnitsInSceneRes);
            unitsInSceneRef.LocalUnitShowReady += OnLocalUnitShowReady;
        }

        if (skillsListRef != null && unitsInSceneRef != null) {
            skillsListRef.UnitsInGameRef = unitsInSceneRef;
            skillsListRef.PlayerInterfaceRes = playerInterfaceRes;
        }

        TryShowLocalUnit();
    }

    /// <summary>
    /// 退出战斗：退订本地单位事件并清理子组件绑定。
    /// </summary>
    public void UnbindFromBattle() {
        unitsInSceneRef?.LocalUnitShowReady -= OnLocalUnitShowReady;
        skillsListRef?.ClearBindings();
    }

    /// <summary>
    /// 本地玩家单位视图就绪：绑定动态友方阵营状态列表并刷新自身技能列表。
    /// </summary>
    /// <param name="localShow">本地玩家控制的单位视图。</param>
    private void OnLocalUnitShowReady(UnitGameShow localShow) {
        if (unitsInSceneRef != null)
            stateBarListRef?.BindUnitsInScene(unitsInSceneRef.UnitsInSceneRes, localShow.Pawn.Camp.Value);
        UpdateSkillList(localShow);
    }

    /// <summary>
    /// 尝试立即展示本地单位：本地单位可能已随单位管理器 Bind 同步生成。
    /// </summary>
    private void TryShowLocalUnit() {
        var localShow = unitsInSceneRef?.LocalUnitShow;
        if (localShow != null)
            OnLocalUnitShowReady(localShow);
    }

    #endregion

    #region Public API

    /// <summary>
    /// 焦点单位变化回调，转发到技能列表刷新。
    /// </summary>
    /// <param name="unitShow">新的焦点单位。</param>
    public void UpdateSkillList(UnitGameShow unitShow) {
        skillsListRef?.UpdateSkillsList(unitShow);
    }

    #endregion
}
