using System;
using System.Collections.Generic;
using DungeonChessBattle.Common;
using DungeonChessBattle.Entities;
using DungeonChessBattle.MainScene;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 状态条列表容器，按阵营展示所有单位的迷你状态条。
/// 每帧从战斗单位管理器直读单位集合与本地阵营，签名变化时重建列表。
/// </summary>
public partial class StateBarList : Control {
    /// <summary>导出引用集合节点。</summary>
    public StateBarListInterRefs? InterRefs {
        get; private set;
    }

    private StateBarListInterRefs InterRefsOrThrow =>
        InterRefs ?? throw new InvalidOperationException("[StateBarList] InterRefs has not been initialized.");

    /// <summary>战斗单位管理器引用，提供单位集合并派生本地玩家阵营。</summary>
    [Export]
    private BattleUnitManager? _unitManagerRef;

    /// <summary>可点击状态条缓存，键为单位网络实体 ID，仅在单位增删时建/删条。</summary>
    private readonly CacheSynchronizer<ushort, UnitPawn, ClickableStateBar> _bars;

    /// <summary>过滤后仅含本地阵营单位的源列表，Sync 每帧复用避免分配。</summary>
    private readonly List<UnitPawn> _filteredUnits = [];

    /// <summary>构造函数：注入键提取、创建、移除与更新回调。</summary>
    public StateBarList() {
        _bars = new(GetKey, CreateBar, RemoveBar, UpdateBar);
    }

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<StateBarListInterRefs>("StateBarListInterRefs");
    }

    /// <summary>提取单位网络实体 ID 作为条键。</summary>
    private static ushort GetKey(UnitPawn pawn) => pawn.Id;

    /// <summary>创建可点击状态条并挂载到列表容器。</summary>
    private ClickableStateBar CreateBar() {
        var bar = InterRefsOrThrow.ClickableStateBarPKS?.Instantiate<ClickableStateBar>()
            ?? throw new InvalidOperationException("[StateBarList] ClickableStateBarPKS is not assigned or instantiation failed.");
        var container = InterRefsOrThrow.VBoxContainerRef
            ?? throw new InvalidOperationException("[StateBarList] VBoxContainerRef is not assigned.");
        bar.UnitManagerRef = _unitManagerRef;
        container.AddChild(bar);
        return bar;
    }

    /// <summary>移除可点击状态条。</summary>
    private static void RemoveBar(ClickableStateBar bar) => bar.QueueFree();

    /// <summary>更新可点击状态条绑定的单位。</summary>
    private static void UpdateBar(ClickableStateBar bar, UnitPawn pawn) => bar.BindUnitState(pawn);

    /// <summary>
    /// 每帧直读单位集合，过滤本地阵营后同步状态条缓存，单位增删时自动建/删条。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var manager = _unitManagerRef;
        if (manager == null || InterRefs == null)
            return;

        var camp = manager.LocalUnitShow?.Pawn.Camp.Value ?? "";
        _filteredUnits.Clear();
        foreach (var unit in manager.UnitsArr) {
            if (unit.Camp.Value == camp)
                _filteredUnits.Add(unit);
        }
        _bars.Sync(_filteredUnits);
    }
}
