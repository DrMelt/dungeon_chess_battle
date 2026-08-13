using System;
using System.Collections.Generic;
using System.Text;
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

    /// <summary>上一帧的列表签名，用于变化检测。</summary>
    private string _lastSignature = "";

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

    /// <summary>
    /// 每帧检查单位列表签名，变化时重建友方状态条列表。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var manager = _unitManagerRef;
        if (manager == null)
            return;

        var camp = manager.LocalUnitShow?.Pawn.Camp.Value ?? "";
        var units = manager.UnitsArr;
        string signature = BuildSignature(units, camp);
        if (signature == _lastSignature)
            return;
        _lastSignature = signature;
        Rebuild(units, camp);
    }

    /// <summary>构建脏检查签名：本地阵营 + 各单位 Id/Camp。</summary>
    private static string BuildSignature(List<UnitPawn> units, string camp) {
        var sb = new StringBuilder();
        sb.Append(camp).Append('|');
        foreach (var unit in units)
            sb.Append(unit.Id).Append(':').Append(unit.Camp.Value).Append(',');
        return sb.ToString();
    }

    /// <summary>清空并重建属于目标阵营的迷你状态条。</summary>
    private void Rebuild(List<UnitPawn> units, string camp) {
        if (InterRefs?.VBoxContainerRef == null)
            return;

        var children = InterRefs.VBoxContainerRef.GetChildren();
        foreach (var child in children) {
            child.QueueFree();
        }

        foreach (var unit in units) {
            if (unit.Camp.Value == camp) {
                StateBarMini stateBarMini = NewStateBarMini;
                InterRefs.VBoxContainerRef.AddChild(stateBarMini);
                stateBarMini.BindUnitState(unit);
            }
        }
    }
}
