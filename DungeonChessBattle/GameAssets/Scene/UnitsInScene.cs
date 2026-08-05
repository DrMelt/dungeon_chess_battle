using Godot;
using Godot.Collections;
using System;


namespace DungeonChessBattle;

/// <summary>
/// 场景单位集合资源，管理所有单位的更新、增删并广播变化事件。
/// </summary>
[GlobalClass]
public partial class UnitsInScene : Resource {
    /// <summary>
    /// 构造函数：初始化单位数组。
    /// </summary>
    public UnitsInScene() {
        unitsArr = [];
    }

    /// <summary>单位间隔更新的时间间隔（秒）。</summary>
    [Export]
    private double updateInterval = 1.0;

    /// <summary>场景中的单位状态数组。</summary>
    [ExportGroup("Runtime Parameters")]
    [Export]
    private Array<UnitState> unitsArr;

    /// <summary>场景单位数组快照。</summary>
    public Array<UnitState> UnitsArr => [.. unitsArr];

    /// <summary>单位集合变化事件。</summary>
    public Action<UnitsInScene>? OnUnitsChangedEvent;

    /// <summary>场景累计时间。</summary>
    [field: Export]
    public double SceneTime { get; private set; } = 0.0;

    /// <summary>上次间隔更新的时间点。</summary>
    [Export]
    private double lastUpdateTime = 0.0;

    /// <summary>
    /// 更新所有单位状态：每帧更新 + 按间隔触发间隔更新。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public void UpdateState(double delta) {
        SceneTime += delta;
        foreach (UnitState unit in unitsArr) {
            unit.UpdateState(delta);
        }

        if (lastUpdateTime + updateInterval < SceneTime) {
            lastUpdateTime += updateInterval;
            foreach (UnitState unit in unitsArr) {
                unit.UpdateStateInterval(updateInterval);
            }
        }
    }

    /// <summary>
    /// 添加单位并广播变化事件。
    /// </summary>
    /// <param name="unitState">要添加的单位状态。</param>
    public void AddUnit(UnitState unitState) {
        unitsArr.Add(unitState);
        OnUnitsChangedEvent?.Invoke(this);
    }

    /// <summary>
    /// 移除单位并广播变化事件。
    /// </summary>
    /// <param name="unitState">要移除的单位状态。</param>
    public void RemoveUnit(UnitState unitState) {
        unitsArr.Remove(unitState);
        OnUnitsChangedEvent?.Invoke(this);
    }

}
