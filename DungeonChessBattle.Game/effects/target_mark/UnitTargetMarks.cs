using System;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.Common;
using DungeonChessBattle.MainScene;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Effects;

/// <summary>
/// 单位目标标记管理器，为场景中单位生成对应的 3D 目标标记并跟随单位位置。
/// 复用已创建的目标标记，仅在单位增删时创建或销毁。
/// 仅本地焦点单位显示标记，未选中隐藏。死亡单位仍可被选中，不影响标记显示。
/// </summary>
public partial class UnitTargetMarks : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitTargetMarks> _logger = ServiceLocator.GetLogger<UnitTargetMarks>();

    /// <summary>战斗会话上下文引用，提供单位集合、焦点单位与阵营关系判定。</summary>
    [Export]
    private BattleSessionContext? _sessionRef;

    /// <summary>3D 目标标记使用的场景资源。</summary>
    [Export]
    private PackedScene? _targetMarkPackedScene;

    /// <summary>标记缓存，键为单位网络实体 ID，回调在构造时注入。</summary>
    private readonly CacheSynchronizer<ushort, IUnitUiView, Node3dTargetMark> _marks;

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
        if (_sessionRef == null)
            _logger.LogError("_sessionRef is not assigned!");
        if (_targetMarkPackedScene == null)
            _logger.LogError("_targetMarkPackedScene is not assigned!");
    }

    /// <summary>
    /// 每帧同步目标标记位置与朝向，并为新增单位创建标记、为移除单位清理标记。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        var session = _sessionRef
            ?? throw new InvalidOperationException("[UnitTargetMarks] _sessionRef is not assigned!");

        _marks.Sync(session.Units);
    }

    /// <summary>提取单位网络实体 ID 作为标记键。</summary>
    private static ushort GetKey(IUnitUiView unit) => unit.UnitNetId;

    /// <summary>创建目标标记并挂载到本节点。</summary>
    private Node3dTargetMark CreateMark() {
        var mark = _targetMarkPackedScene?.Instantiate<Node3dTargetMark>()
            ?? throw new InvalidOperationException("[UnitTargetMarks] _targetMarkPackedScene is not assigned!");
        AddChild(mark);
        return mark;
    }

    /// <summary>移除目标标记。</summary>
    private static void RemoveMark(Node3dTargetMark mark) => mark.QueueFree();

    /// <summary>阵营关系未知一次性告警标记，避免刷屏。</summary>
    private bool _unknownRelationLogged;

    /// <summary>
    /// 更新目标标记：仅本地焦点单位显示并同步半径、阵营颜色、位置与朝向，其余单位隐藏。
    /// 选中判据仅与本地焦点单位关联，不因单位死亡而变化。
    /// </summary>
    private void UpdateMark(Node3dTargetMark mark, IUnitUiView unit) {
        var session = _sessionRef;
        var focusUnit = session?.LocalFocus;
        bool isFocus = unit == focusUnit;
        if (isFocus != mark.Visible && _logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Mark unit={UnitId}: visible={Visible}, radius={Radius}",
                unit.UnitNetId, isFocus, unit.BodyRadius);

        mark.Visible = isFocus;
        mark.SetRadius(unit.BodyRadius);
        var relation = session?.ResolveLocalCampRelation(unit.Camps) ?? CampRelation.Unknown;
        mark.SetColor(relation);
        if (relation == CampRelation.Unknown && !_unknownRelationLogged) {
            _unknownRelationLogged = true;
            _logger.LogWarning("[UnitTargetMarks] 阵营关系未知，目标标记着色置灰。");
        }
        var pos = unit.Position;
        mark.GlobalPosition = new Vector3(pos.X, 0f, pos.Y);

        var dir = unit.Direction;
        if (dir.LengthSquared() > 0.0001f) {
            mark.LookAt(mark.GlobalPosition + new Vector3(dir.X, 0f, dir.Y));
        }
    }
}
