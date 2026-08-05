using Godot;
using DungeonChessBattle.Core.Enums;
using System;

namespace DungeonChessBattle;

/// <summary>
/// 状态变化信息管理器，订阅单位事件并在对应位置弹出受击/ Buff 增减提示。
/// </summary>
public partial class StateChangeInfo : Node {
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

    /// <summary>上一帧的单位列表，用于解绑已消失单位的订阅。</summary>
    private Godot.Collections.Array<UnitState>? preUnits;

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<StateChangeInfoInterRefs>("StateChangeInfoInterRefs");
        if (InterRefs == null) {
            GD.PrintErr("[StateChangeInfo] StateChangeInfoInterRefs node not found.");
        }
    }

    /// <summary>
    /// 订阅场景单位集合变化事件。
    /// </summary>
    /// <param name="unitsInSceneRes">场景单位集合。</param>
    public void BindUnitsInScene(UnitsInScene unitsInSceneRes) {
        unitsInSceneRes.OnUnitsChangedEvent += OnUnitsInSceneChanged;
    }

    /// <summary>
    /// 单位集合变化回调：解绑旧单位事件并绑定新单位事件。
    /// </summary>
    /// <param name="unitsInScene">场景单位集合。</param>
    private void OnUnitsInSceneChanged(UnitsInScene unitsInScene) {
        if (preUnits != null) {
            foreach (var unit in preUnits) {
                {
                    UnbindWithUnitState(unit);
                }
            }
        }

        Godot.Collections.Array<UnitState> units = unitsInScene.UnitsArr;
        foreach (var unit in units) {
            BindWithUnitState(unit);
        }
        preUnits = units;
    }

    /// <summary>
    /// 订阅单个单位的受击与 Buff 事件。
    /// </summary>
    /// <param name="unitState">目标单位状态。</param>
    private void BindWithUnitState(UnitState unitState) {
        unitState.OnBuffAddedEvent += OnUnitBuffAdded;
        unitState.OnBuffRemovedEvent += OnUnitBuffRemoved;
        unitState.OnTookDamageEvent += OnUnitTookDamage;
    }

    /// <summary>
    /// 取消订阅单个单位的事件。
    /// </summary>
    /// <param name="unitState">目标单位状态。</param>
    private void UnbindWithUnitState(UnitState unitState) {
        unitState.OnBuffAddedEvent -= OnUnitBuffAdded;
        unitState.OnBuffRemovedEvent -= OnUnitBuffRemoved;
        unitState.OnTookDamageEvent -= OnUnitTookDamage;
    }

    /// <summary>
    /// 单位获得 Buff 回调：在单位位置弹出添加提示。
    /// </summary>
    /// <param name="unitState">目标单位状态。</param>
    /// <param name="buff">被添加的 Buff。</param>
    private void OnUnitBuffAdded(UnitState unitState, BuffBaseGodot buff) {
        BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
        AddChild(buffChangeInfo);
        buffChangeInfo.Init(buff, BuffChangeInfo.Enum_BuffChangeType.Added);
        buffChangeInfo.GlobalPosition = WorldToScreenPos(this, unitState.Position + Vector3.Up * 2.2f);
    }

    /// <summary>
    /// 单位移除 Buff 回调：在单位位置弹出移除提示。
    /// </summary>
    /// <param name="unitState">目标单位状态。</param>
    /// <param name="buff">被移除的 Buff。</param>
    private void OnUnitBuffRemoved(UnitState unitState, BuffBaseGodot buff) {
        BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
        AddChild(buffChangeInfo);
        buffChangeInfo.Init(buff, BuffChangeInfo.Enum_BuffChangeType.Removed);
        buffChangeInfo.GlobalPosition = WorldToScreenPos(this, unitState.Position + Vector3.Up * 2.2f);
    }

    /// <summary>
    /// 单位受击回调：在单位位置弹出受击伤害提示。
    /// </summary>
    /// <param name="unitState">目标单位状态。</param>
    /// <param name="damage">伤害数值。</param>
    /// <param name="damageType">伤害类型。</param>
    private void OnUnitTookDamage(UnitState unitState, float damage, DamageType damageType) {
        TookDamageInfo tookDamageInfo = NewTookDamageInfo;
        AddChild(tookDamageInfo);
        var uiSettings = InterRefsOrThrow.PlayerUISettingsRes
            ?? throw new InvalidOperationException("[StateChangeInfo] PlayerUISettingsRes is not assigned in InterRefs.");
        tookDamageInfo.Init(damage, damageType, uiSettings);
        tookDamageInfo.GlobalPosition = WorldToScreenPos(this, unitState.Position + Vector3.Up * 2.2f);
    }
}
