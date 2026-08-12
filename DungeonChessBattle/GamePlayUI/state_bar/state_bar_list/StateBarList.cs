using System;
using DungeonChessBattle.GameAssets;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 状态条列表容器，按阵营展示所有单位的迷你状态条，随场景单位变化自动刷新。
/// </summary>
public partial class StateBarList : Control {
    /// <summary>导出引用集合节点。</summary>
    public StateBarListInterRefs? InterRefs {
        get; private set;
    }

    private StateBarListInterRefs InterRefsOrThrow =>
        InterRefs ?? throw new InvalidOperationException("[StateBarList] InterRefs has not been initialized.");

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<StateBarListInterRefs>("StateBarListInterRefs");
    }

    /// <summary>实例化一个迷你状态条。</summary>
    private StateBarMini NewStateBarMini =>
        InterRefsOrThrow.StateBarMiniPKS?.Instantiate<StateBarMini>()
        ?? throw new InvalidOperationException("[StateBarList] StateBarMiniPKS is not assigned or instantiation failed.");

    /// <summary>当前绑定的场景单位集合。</summary>
    private UnitsInScene? bindingUnitsInScene;

    /// <summary>要展示的友方阵营标识，由进入战斗时本地玩家阵营注入。</summary>
    private string _localCamp = "";

    /// <summary>
    /// 绑定场景单位集合并订阅单位变化事件，立即刷新一次。
    /// </summary>
    /// <param name="unitsInScene">场景单位集合。</param>
    /// <param name="localCamp">本地玩家所在阵营标识，仅展示该阵营的友方单位。</param>
    public void BindUnitsInScene(UnitsInScene unitsInScene, string localCamp) {
        bindingUnitsInScene?.OnUnitsChangedEvent -= OnUnitsChanged;
        bindingUnitsInScene = unitsInScene;
        _localCamp = localCamp;

        bindingUnitsInScene.OnUnitsChangedEvent += OnUnitsChanged;
        OnUnitsChanged(bindingUnitsInScene);
    }

    /// <summary>
    /// 单位集合变化回调：清空并重建属于目标阵营的迷你状态条。
    /// </summary>
    /// <param name="scene">场景单位集合。</param>
    private void OnUnitsChanged(UnitsInScene scene) {
        if (InterRefs?.VBoxContainerRef == null)
            return;
        var children = InterRefs.VBoxContainerRef.GetChildren();
        foreach (var child in children) {
            child.QueueFree();
        }

        var units = scene.UnitsArr;
        foreach (var unit in units) {
            if (unit.Camp.Value == _localCamp) {
                StateBarMini stateBarMini = NewStateBarMini;

                InterRefs.VBoxContainerRef.AddChild(stateBarMini);
                stateBarMini.BindUnitState(unit);
            }
        }
    }

}
