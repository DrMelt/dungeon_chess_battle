using System;
using DungeonChessBattle.Common;
using DungeonChessBattle.Entities;
using DungeonChessBattle.MainScene;
using Godot;

namespace DungeonChessBattle.Effects;

/// <summary>
/// 单位目标标记管理器，为场景中所有单位生成对应的 3D 目标标记并跟随单位位置。
/// 复用已创建的目标标记，仅在单位增删时创建或销毁。
/// </summary>
public partial class UnitTargetMarks : Node {
    /// <summary>战斗单位管理器引用。</summary>
    [Export]
    private BattleUnitManager? _unitManagerRef;

    /// <summary>3D 目标标记使用的场景资源。</summary>
    [Export]
    private PackedScene? _targetMarkPackedScene;

    /// <summary>标记缓存，键为单位网络实体 ID，回调在构造时注入。</summary>
    private readonly KeyedCache<ushort, UnitPawn, Node3dTargetMark> _marks;

    /// <summary>
    /// 构造函数：注入键提取、创建、移除与更新回调。
    /// </summary>
    public UnitTargetMarks() {
        _marks = new(GetKey, CreateMark, RemoveMark, UpdateMark);
    }

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (_unitManagerRef == null)
            GD.PrintErr("[UnitTargetMarks] [Export] _unitManagerRef is not assigned!");
        if (_targetMarkPackedScene == null)
            GD.PrintErr("[UnitTargetMarks] [Export] _targetMarkPackedScene is not assigned!");
    }

    /// <summary>
    /// 每帧同步目标标记位置与朝向，并为新增单位创建标记、为移除单位清理标记。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var manager = _unitManagerRef
            ?? throw new InvalidOperationException("[UnitTargetMarks] _unitManagerRef is not assigned!");

        _marks.Sync(manager.UnitsArr);
    }

    /// <summary>提取单位网络实体 ID 作为标记键。</summary>
    private static ushort GetKey(UnitPawn pawn) => pawn.Id;

    /// <summary>创建目标标记并挂载到本节点。</summary>
    private Node3dTargetMark CreateMark() {
        var mark = _targetMarkPackedScene?.Instantiate<Node3dTargetMark>()
            ?? throw new InvalidOperationException("[UnitTargetMarks] _targetMarkPackedScene is not assigned!");
        AddChild(mark);
        return mark;
    }

    /// <summary>移除目标标记。</summary>
    private static void RemoveMark(Node3dTargetMark mark) => mark.QueueFree();

    /// <summary>更新目标标记的半径、阵营颜色、位置与朝向。</summary>
    private static void UpdateMark(Node3dTargetMark mark, UnitPawn pawn) {
        mark.SetRadius(pawn.BodyRadius.Value);
        mark.SetCampColor(pawn.Camp.Value);
        var pos = pawn.Position.InterpolatedValue;
        mark.GlobalPosition = new Vector3(pos.X, 0f, pos.Y);

        var dir = pawn.Direction.InterpolatedValue;
        if (dir.LengthSquared() > 0.0001f) {
            mark.LookAt(mark.GlobalPosition + new Vector3(dir.X, 0f, dir.Y));
        }
    }
}
