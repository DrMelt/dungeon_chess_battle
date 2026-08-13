using System;
using DungeonChessBattle.Common;
using DungeonChessBattle.Entities;
using DungeonChessBattle.GamePlayUI;
using DungeonChessBattle.MainScene;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Effects;

/// <summary>
/// 单位目标标记管理器，为场景中单位生成对应的 3D 目标标记并跟随单位位置。
/// 复用已创建的目标标记，仅在单位增删时创建或销毁。
/// 仅选中单位显示标记，未选中隐藏。死亡单位仍可被选中，不影响标记显示。
/// </summary>
public partial class UnitTargetMarks : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitTargetMarks> _logger = ServiceLocator.GetLogger<UnitTargetMarks>();

    /// <summary>战斗单位管理器引用。</summary>
    [Export]
    private BattleUnitManager? _unitManagerRef;

    /// <summary>3D 目标标记使用的场景资源。</summary>
    [Export]
    private PackedScene? _targetMarkPackedScene;

    /// <summary>玩家界面资源引用，用于读取当前选中的单位。</summary>
    [Export]
    private PlayerInterfaceRes? _playerInterfaceResRef;

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
            _logger.LogError("_unitManagerRef is not assigned!");
        if (_targetMarkPackedScene == null)
            _logger.LogError("_targetMarkPackedScene is not assigned!");
        if (_playerInterfaceResRef == null)
            _logger.LogError("_playerInterfaceResRef is not assigned!");
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

    /// <summary>
    /// 更新目标标记：仅选中单位显示并同步半径、阵营颜色、位置与朝向，其余单位隐藏。
    /// 选中判据仅与 FocusOnUnit 关联，不因单位死亡而变化。
    /// </summary>
    private void UpdateMark(Node3dTargetMark mark, UnitPawn pawn) {
        var focusPawn = _playerInterfaceResRef?.FocusOnUnit?.Pawn;
        bool isFocus = pawn == focusPawn;
        if (isFocus != mark.Visible && _logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Mark unit={UnitId}: visible={Visible}, radius={Radius}",
                pawn.Id, isFocus, pawn.BodyRadius.Value);

        if (!isFocus) {
            mark.Visible = false;
            return;
        }

        mark.Visible = true;
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
