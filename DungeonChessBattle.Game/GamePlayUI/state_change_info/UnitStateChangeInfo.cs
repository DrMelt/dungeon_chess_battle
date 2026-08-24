using System;
using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.MainScene;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;
using DamageType = DungeonChessBattle.Battle.Shared.Combat.DamageType;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 状态变化信息管理器，订阅战斗事件日志流并在对应位置弹出受击/治疗/Buff 增减提示。
/// 瞬时表现数据源为服务端权威事件日志；HP/Buff 等状态展示以 SyncVar 为准，本组件不投影状态。
/// </summary>
public partial class UnitStateChangeInfo : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitStateChangeInfo> _logger = ServiceLocator.GetLogger<UnitStateChangeInfo>();

    /// <summary>战斗会话上下文引用，提供场景单位集合与服务。</summary>
    [Export]
    private BattleSessionContext? _sessionRef;

    /// <summary>状态变化提示的缩放系数。</summary>
    [Export]
    private float _popupScale = 1f;

    /// <summary>当前已订阅事件流的战斗服务，进出战斗时切换。</summary>
    private IClientBattleService? _boundService;

    /// <summary>当前房间 ID，用于事件过滤。</summary>
    private string _roomId = "";

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

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<UnitStateChangeInfoInterRefs>("UnitStateChangeInfoInterRefs");
        if (InterRefs == null) {
            _logger.LogError("StateChangeInfoInterRefs node not found.");
        }
        if (_sessionRef == null)
            _logger.LogError("_sessionRef is not assigned!");
    }

    /// <summary>
    /// 每帧同步事件流订阅：进出战斗时切换绑定的战斗服务，避免跨战斗悬挂。
    /// </summary>
    public override void _Process(double delta) {
        var session = _sessionRef;
        if (session == null)
            return;

        var service = session.BattleService;
        if (service != _boundService) {
            _boundService?.BattleEventsReceived -= OnBattleEventsReceived;
            _boundService = service;
            service?.BattleEventsReceived += OnBattleEventsReceived;
        }
        _roomId = session.RoomId;
    }

    /// <summary>节点退出场景树：兜底退订事件流，防止战斗中途场景被释放导致事件悬挂。</summary>
    public override void _ExitTree() {
        _boundService?.BattleEventsReceived -= OnBattleEventsReceived;
        _boundService = null;
    }

    /// <summary>
    /// 战斗事件日志订阅：按事件类型在对应单位位置弹出现时表现提示。
    /// </summary>
    private void OnBattleEventsReceived(string roomId, IReadOnlyList<IBattleEvent> events) {
        if (roomId != _roomId)
            return;

        foreach (var battleEvent in events) {
            switch (battleEvent) {
                case DamageOccurred dmg:
                    if (FindPawn(dmg.TargetNetId) is { } dmgPawn)
                        ShowDamagePopup(dmgPawn, dmg.AppliedDamage, dmg.DamageType);
                    break;

                case HealOccurred heal:
                    if (FindPawn(heal.TargetNetId) is { } healPawn)
                        ShowHealPopup(healPawn, heal.ActualHeal);
                    break;

                case BuffApplied buff:
                    if (FindPawn(buff.TargetNetId) is { } buffPawn)
                        ShowBuffPopup(buffPawn, buff.BuffTypeId, added: true);
                    break;

                case BuffExpired expired:
                    if (FindPawn(expired.TargetNetId) is { } expPawn)
                        ShowBuffPopup(expPawn, expired.BuffTypeId, added: false);
                    break;
            }
        }
    }

    /// <summary>按网络实体 ID 查找场景单位。</summary>
    private UnitPawn? FindPawn(ushort netId) {
        var session = _sessionRef;
        if (session == null)
            return null;
        foreach (var unit in session.Units) {
            if (unit.Id == netId)
                return unit;
        }
        return null;
    }

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
    private void ShowDamagePopup(UnitPawn pawn, float damage, DamageType damageType) {
        TookDamageInfo tookDamageInfo = NewTookDamageInfo;
        AddChild(tookDamageInfo);
        ApplyPopupScale(tookDamageInfo);
        var uiSettings = InterRefsOrThrow.PlayerUISettingsRes
            ?? throw new InvalidOperationException("[StateChangeInfo] PlayerUISettingsRes is not assigned in InterRefs.");
        tookDamageInfo.Init(damage, damageType, uiSettings);
        PopupAtUnit(tookDamageInfo, pawn);
    }

    /// <summary>
    /// 单位治疗提示：在单位位置弹出治疗浮字。
    /// </summary>
    private void ShowHealPopup(UnitPawn pawn, float heal) {
        TookDamageInfo healInfo = NewTookDamageInfo;
        AddChild(healInfo);
        ApplyPopupScale(healInfo);
        var uiSettings = InterRefsOrThrow.PlayerUISettingsRes
            ?? throw new InvalidOperationException("[StateChangeInfo] PlayerUISettingsRes is not assigned in InterRefs.");
        healInfo.Init(heal, uiSettings.HealthInfoColor);
        PopupAtUnit(healInfo, pawn);
    }

    /// <summary>
    /// 单位 Buff 提示：在单位位置弹出 Buff 添加/移除浮字，图标按 BuffTypeId 从资源表匹配。
    /// </summary>
    private void ShowBuffPopup(UnitPawn pawn, ushort buffTypeId, bool added) {
        BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
        AddChild(buffChangeInfo);
        ApplyPopupScale(buffChangeInfo);
        var buffData = new SyncBuffData { BuffTypeId = buffTypeId };
        buffChangeInfo.Init(buffData, added
            ? BuffChangeInfo.Enum_BuffChangeType.Added
            : BuffChangeInfo.Enum_BuffChangeType.Removed);
        PopupAtUnit(buffChangeInfo, pawn);
    }

    /// <summary>把提示节点定位到单位头顶。</summary>
    private static void PopupAtUnit(Control info, UnitPawn pawn) {
        var pos = pawn.Position.InterpolatedValue;
        info.GlobalPosition = WorldToScreenPos(info, new Vector3(pos.X, 0f, pos.Y) + Vector3.Up * 2.2f);
    }
}
