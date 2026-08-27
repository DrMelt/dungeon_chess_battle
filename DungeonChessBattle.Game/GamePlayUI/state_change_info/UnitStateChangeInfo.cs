using System;
using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Entities.SyncData;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;
using DamageType = DungeonChessBattle.Battle.Shared.Combat.DamageType;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 状态变化信息渲染器：把 <see cref="IBattleEvent"/> 流渲染为单位头顶的受击/治疗/Buff 增减浮字。
/// 纯表现组件：单位取数经注入的 <see cref="IBattleViewSource"/>，事件由驱动方（在线/回放编排器）喂入。
/// 在线与回放共用此唯一实例，UI 不感知事件来源。
/// </summary>
public partial class UnitStateChangeInfo : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitStateChangeInfo> _logger = ServiceLocator.GetLogger<UnitStateChangeInfo>();

    /// <summary>状态变化提示的缩放系数。</summary>
    [Export]
    private float _popupScale = 1f;

    /// <summary>当前展示数据源（在线为状态镜像、回放为回放引擎），用于单位取数。</summary>
    private IBattleViewSource? _viewSource;

    /// <summary>浮字容器：承载全部表现浮字，浮字淡出后自行销毁，容器随本节点释放。</summary>
    private Node? _effects_root;

    /// <summary>导出引用集合节点。</summary>
    public UnitStateChangeInfoInterRefs? InterRefs {
        get; private set;
    }

    private UnitStateChangeInfoInterRefs InterRefsOrThrow =>
        InterRefs ?? throw new InvalidOperationException("[StateChangeInfo] InterRefs has not been initialized.");

    /// <summary>实例化一个受击提示。</summary>
    private TookDamageInfo NewTookDamageInfo =>
        InterRefsOrThrow.TookDamageInfoPackedScene?.Instantiate<TookDamageInfo>()
        ?? throw new InvalidOperationException("[StateChangeInfo] TookDamageInfoPackedScene is not assigned or instantiation failed.");

    /// <summary>实例化一个 Buff 变化提示。</summary>
    private BuffChangeInfo NewBuffChangeInfo =>
        InterRefsOrThrow.BuffChangeInfoPackedScene?.Instantiate<BuffChangeInfo>()
        ?? throw new InvalidOperationException("[StateChangeInfo] BuffChangeInfoPackedScene is not assigned or instantiation failed.");

    /// <summary>节点就绪：获取引用集合节点。</summary>
    public override void _Ready() {
        InterRefs = GetNode<UnitStateChangeInfoInterRefs>("UnitStateChangeInfoInterRefs");
        if (InterRefs == null)
            _logger.LogError("StateChangeInfoInterRefs node not found.");
        _effects_root = new Node { Name = "EffectsRoot" };
        AddChild(_effects_root);
    }

    /// <summary>绑定展示数据源。</summary>
    public void Bind(IBattleViewSource source) {
        _viewSource = source;
    }

    /// <summary>解绑展示数据源。</summary>
    public void Unbind() {
        _viewSource = null;
    }

    /// <summary>
    /// 消费一帧战斗事件：按事件类型在目标单位位置弹出现时表现提示。由驱动方（在线/回放编排器）喂入。
    /// </summary>
    public void Consume(IReadOnlyList<IBattleEvent> events) {
        foreach (var battleEvent in events) {
            switch (battleEvent) {
                case DamageOccurred dmg:
                    if (FindUnit(dmg.TargetNetId) is { } dmgPawn)
                        ShowDamagePopup(dmgPawn, dmg.AppliedDamage, dmg.DamageType);
                    break;

                case HealOccurred heal:
                    if (FindUnit(heal.TargetNetId) is { } healPawn)
                        ShowHealPopup(healPawn, heal.ActualHeal);
                    break;

                case BuffApplied buff:
                    if (FindUnit(buff.TargetNetId) is { } buffPawn)
                        ShowBuffPopup(buffPawn, buff.BuffTypeId, added: true);
                    break;

                case BuffExpired expired:
                    if (FindUnit(expired.TargetNetId) is { } expPawn)
                        ShowBuffPopup(expPawn, expired.BuffTypeId, added: false);
                    break;
            }
        }
    }

    /// <summary>按网络实体 ID 查找展示单位，来源为注入的展示数据源。</summary>
    private IUnitUiView? FindUnit(ushort netId) => _viewSource?.FindUnit(netId);

    /// <summary>
    /// 将世界坐标投影为屏幕坐标。
    /// </summary>
    /// <param name="node">用于获取视口的节点。</param>
    /// <param name="worldPos">世界坐标。</param>
    /// <returns>屏幕坐标。</returns>
    private static Vector2 WorldToScreenPos(Node node, Vector3 worldPos) {
        var camera3D = node.GetViewport().GetCamera3D();
        var screenPos = camera3D.UnprojectPosition(worldPos);
        return screenPos;
    }

    /// <summary>按缩放配置设置提示节点的缩放。</summary>
    /// <param name="info">提示节点。</param>
    private void ApplyPopupScale(Control info) {
        info.Scale = new Vector2(_popupScale, _popupScale);
    }

    /// <summary>
    /// 单位受击提示：在单位位置弹出受击伤害浮字。
    /// </summary>
    private void ShowDamagePopup(IUnitUiView unit, float damage, DamageType damageType) {
        TookDamageInfo tookDamageInfo = NewTookDamageInfo;
        _effects_root?.AddChild(tookDamageInfo);
        ApplyPopupScale(tookDamageInfo);
        var uiSettings = InterRefsOrThrow.PlayerUISettingsRes
            ?? throw new InvalidOperationException("[StateChangeInfo] PlayerUISettingsRes is not assigned in InterRefs.");
        tookDamageInfo.Init(damage, damageType, uiSettings);
        PopupAtUnit(tookDamageInfo, unit);
    }

    /// <summary>
    /// 单位治疗提示：在单位位置弹出治疗浮字。
    /// </summary>
    private void ShowHealPopup(IUnitUiView unit, float heal) {
        TookDamageInfo healInfo = NewTookDamageInfo;
        _effects_root?.AddChild(healInfo);
        ApplyPopupScale(healInfo);
        var uiSettings = InterRefsOrThrow.PlayerUISettingsRes
            ?? throw new InvalidOperationException("[StateChangeInfo] PlayerUISettingsRes is not assigned in InterRefs.");
        healInfo.Init(heal, uiSettings.HealthInfoColor);
        PopupAtUnit(healInfo, unit);
    }

    /// <summary>
    /// 单位 Buff 提示：在单位位置弹出 Buff 添加/移除浮字，图标按 BuffTypeId 从资源表匹配。
    /// </summary>
    private void ShowBuffPopup(IUnitUiView unit, ushort buffTypeId, bool added) {
        BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
        _effects_root?.AddChild(buffChangeInfo);
        ApplyPopupScale(buffChangeInfo);
        var buffData = new SyncBuffData { BuffTypeId = buffTypeId };
        buffChangeInfo.Init(buffData, added
            ? BuffChangeInfo.Enum_BuffChangeType.Added
            : BuffChangeInfo.Enum_BuffChangeType.Removed);
        PopupAtUnit(buffChangeInfo, unit);
    }

    /// <summary>把提示节点定位到单位头顶。</summary>
    private static void PopupAtUnit(Control info, IUnitUiView unit) {
        var pos = unit.Position;
        info.GlobalPosition = WorldToScreenPos(info, new Vector3(pos.X, 0f, pos.Y) + Vector3.Up * 2.2f);
    }
}
