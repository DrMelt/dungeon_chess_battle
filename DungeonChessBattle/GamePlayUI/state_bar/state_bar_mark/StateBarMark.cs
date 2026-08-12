using System;
using DungeonChessBattle.Common;
using DungeonChessBattle.Entities;
using DungeonChessBattle.MainScene;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 状态标记容器，为场景中所有单位生成对应的 2D 状态标记。
/// 复用已创建的状态标记，仅在单位增删时创建或销毁。
/// </summary>
public partial class StateBarMark : Control {
    /// <summary>战斗单位管理器引用。</summary>
    [Export]
    private BattleUnitManager? unitsInSceneRef;
    /// <summary>2D 状态标记使用的场景资源。</summary>
    [Export]
    private PackedScene? stateBarSimple2d_PKD;

    /// <summary>标记缓存，键为单位网络实体 ID，回调在构造时注入。</summary>
    private readonly KeyedCache<ushort, UnitPawn, StateBarMark2d> _marks;

    /// <summary>
    /// 构造函数：注入键提取、创建、移除与更新回调。
    /// </summary>
    public StateBarMark() {
        _marks = new(GetKey, CreateMark, RemoveMark, UpdateMark);
    }

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (unitsInSceneRef == null)
            GD.PrintErr("[StateBarMark] [Export] unitsInSceneRef is not assigned!");
        if (stateBarSimple2d_PKD == null)
            GD.PrintErr("[StateBarMark] [Export] stateBarSimple2d_PKD is not assigned!");
    }

    /// <summary>
    /// 每帧同步所有单位的状态标记，并为新增单位创建标记、为移除单位清理标记。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var manager = unitsInSceneRef
            ?? throw new InvalidOperationException("[StateBarMark] unitsInSceneRef is not assigned!");

        _marks.Sync(manager.UnitsArr);
    }

    /// <summary>提取单位网络实体 ID 作为标记键。</summary>
    private static ushort GetKey(UnitPawn pawn) => pawn.Id;

    /// <summary>创建状态标记并挂载到本节点。</summary>
    private StateBarMark2d CreateMark() {
        var mark = stateBarSimple2d_PKD?.Instantiate<StateBarMark2d>()
            ?? throw new InvalidOperationException("[StateBarMark] stateBarSimple2d_PKD is not assigned!");
        AddChild(mark);
        return mark;
    }

    /// <summary>移除状态标记。</summary>
    private static void RemoveMark(StateBarMark2d mark) => mark.QueueFree();

    /// <summary>更新状态标记的位置与血条显示。</summary>
    private static void UpdateMark(StateBarMark2d mark, UnitPawn pawn) {
        mark.UpdateUI_WithUnit(pawn);
    }
}
