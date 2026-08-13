using System;
using System.Collections.Generic;
using DungeonChessBattle.Entities;
using DungeonChessBattle.MainScene;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;
using DamageType = DungeonChessBattle.Battle.Domain.Combat.DamageType;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 状态变化信息管理器，订阅单位 Pawn 事件并在对应位置弹出受击/ Buff 增减提示。
/// </summary>
public partial class StateChangeInfo : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<StateChangeInfo> _logger = ServiceLocator.GetLogger<StateChangeInfo>();

    /// <summary>
    /// 将世界坐标投影为屏幕坐标。
    /// </summary>
    /// <param name="node">用于获取视口的节点。</param>
    /// <param name="wordPos">世界坐标。</param>
    /// <returns>屏幕坐标。</returns>
    private static Vector2 WorldToScreenPos(Node node, Vector3 wordPos) {
        var camera3D = node.GetViewport().GetCamera3D();
        var screenPos = camera3D.UnprojectPosition(wordPos);
        return screenPos;
    }

    /// <summary>导出引用集合节点。</summary>
    public StateChangeInfoInterRefs? InterRefs {
        get; private set;
    }

    private StateChangeInfoInterRefs InterRefsOrThrow =>
        InterRefs ?? throw new InvalidOperationException("[StateChangeInfo] InterRefs has not been initialized.");

    /// <summary>实例化一个受击提示。</summary>
    private TookDamageInfo NewTookDamageInfo =>
        InterRefsOrThrow.TookDamageInfoPackedScene?.Instantiate<TookDamageInfo>()
        ?? throw new InvalidOperationException("[StateChangeInfo] TookDamageInfoPackedScene is not assigned or instantiation failed.");

    /// <summary>实例化一个 Buff 变化提示。</summary>
    private BuffChangeInfo NewBuffChangeInfo =>
        InterRefsOrThrow.BuffChangeInfoPackedScene?.Instantiate<BuffChangeInfo>()
        ?? throw new InvalidOperationException("[StateChangeInfo] BuffChangeInfoPackedScene is not assigned or instantiation failed.");

    /// <summary>战斗单位管理器引用，提供场景单位集合。</summary>
    [Export]
    private BattleUnitManager? _unitManagerRef;

    /// <summary>已订阅 Pawn 事件的单位映射，用于增量同步。</summary>
    private readonly Dictionary<ushort, UnitPawn> _boundPawns = [];

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<StateChangeInfoInterRefs>("StateChangeInfoInterRefs");
        if (InterRefs == null) {
            _logger.LogError("StateChangeInfoInterRefs node not found.");
        }
    }

    /// <summary>
    /// 每帧将 Pawn 事件订阅同步到当前场景单位集合：新增单位绑定、消失单位退订。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var manager = _unitManagerRef;
        if (manager == null)
            return;

        var currentIds = new HashSet<ushort>();
        foreach (var unit in manager.UnitsArr) {
            currentIds.Add(unit.Id);
            if (!_boundPawns.ContainsKey(unit.Id)) {
                BindWithUnitPawn(unit);
                _boundPawns[unit.Id] = unit;
            }
        }

        if (_boundPawns.Count == currentIds.Count)
            return;
        List<ushort> gone = [];
        foreach (var id in _boundPawns.Keys)
            if (!currentIds.Contains(id))
                gone.Add(id);
        foreach (var id in gone) {
            UnbindWithUnitPawn(_boundPawns[id]);
            _boundPawns.Remove(id);
        }
    }

    /// <summary>
    /// 订阅单个单位 Pawn 的受击与 Buff 事件。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    private void BindWithUnitPawn(UnitPawn pawn) {
        pawn.BuffAdded += OnUnitBuffAdded;
        pawn.BuffRemoved += OnUnitBuffRemoved;
        pawn.TookDamage += OnUnitTookDamage;
    }

    /// <summary>
    /// 取消订阅单个单位 Pawn 的事件。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    private void UnbindWithUnitPawn(UnitPawn pawn) {
        pawn.BuffAdded -= OnUnitBuffAdded;
        pawn.BuffRemoved -= OnUnitBuffRemoved;
        pawn.TookDamage -= OnUnitTookDamage;
    }

    /// <summary>
    /// 单位获得 Buff 回调：在单位位置弹出添加提示。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    /// <param name="buff">被添加的同步 Buff 数据。</param>
    private void OnUnitBuffAdded(UnitPawn pawn, Entities.SyncData.SyncBuffData buff) {
        BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
        AddChild(buffChangeInfo);
        buffChangeInfo.Init(buff, BuffChangeInfo.Enum_BuffChangeType.Added);
        var pos = pawn.Position.InterpolatedValue;
        buffChangeInfo.GlobalPosition = WorldToScreenPos(this, new Vector3(pos.X, 0f, pos.Y) + Vector3.Up * 2.2f);
    }

    /// <summary>
    /// 单位移除 Buff 回调：在单位位置弹出移除提示。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    /// <param name="buff">被移除的同步 Buff 数据。</param>
    private void OnUnitBuffRemoved(UnitPawn pawn, Entities.SyncData.SyncBuffData buff) {
        BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
        AddChild(buffChangeInfo);
        buffChangeInfo.Init(buff, BuffChangeInfo.Enum_BuffChangeType.Removed);
        var pos = pawn.Position.InterpolatedValue;
        buffChangeInfo.GlobalPosition = WorldToScreenPos(this, new Vector3(pos.X, 0f, pos.Y) + Vector3.Up * 2.2f);
    }

    /// <summary>
    /// 单位受击回调：在单位位置弹出受击伤害提示。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    /// <param name="damage">伤害数值。</param>
    /// <param name="damageType">伤害类型。</param>
    private void OnUnitTookDamage(UnitPawn pawn, float damage, DamageType damageType) {
        TookDamageInfo tookDamageInfo = NewTookDamageInfo;
        AddChild(tookDamageInfo);
        var uiSettings = InterRefsOrThrow.PlayerUISettingsRes
            ?? throw new InvalidOperationException("[StateChangeInfo] PlayerUISettingsRes is not assigned in InterRefs.");
        tookDamageInfo.Init(damage, damageType, uiSettings);
        var pos = pawn.Position.InterpolatedValue;
        tookDamageInfo.GlobalPosition = WorldToScreenPos(this, new Vector3(pos.X, 0f, pos.Y) + Vector3.Up * 2.2f);
    }
}
