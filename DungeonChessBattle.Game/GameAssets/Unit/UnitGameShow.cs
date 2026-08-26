using System;
using Godot;
using Godot.Collections;
using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 单位 3D 展示组件。
/// 绑定本地展示视图（<see cref="IUnitUiView"/>），每帧直读位置/朝向驱动网格。
/// 技能展示资源（UnitSkillBaseGodot）由 UnitShowManager 注入。
/// </summary>
public partial class UnitGameShow : Node3D {
    /// <summary>本地展示视图（运行时注入，由 UnitShowManager.SpawnUnit 赋值）。</summary>
    private IUnitUiView? _unit;

    /// <summary>本地展示视图。</summary>
    public IUnitUiView Unit {
        get => _unit ?? throw new InvalidOperationException("Unit has not been assigned.");
        set => _unit = value;
    }

    /// <summary>单位技能展示列表（Godot Resource，由 UnitShowManager 注入）。</summary>
    public Array<UnitSkillBaseGodot> SkillsList { get; set; } = [];

    /// <summary>导出引用集合节点。</summary>
    private UnitGameShowInterRefs? _interRefs;


    /// <summary>单位网格实例。</summary>
    public MeshInstance3D? UnitMeshInstanceRef => _interRefs?.UnitMeshInstanceRef;

    /// <summary>单位点击交互区域。</summary>
    public UnitShowArea3D? UnitShowAreaRef => _interRefs?.UnitShowAreaRef;

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        _interRefs = GetNode<UnitGameShowInterRefs>("UnitGameShowInterRefs");
    }

    /// <summary>
    /// 获取当前正在施放的单位技能，用于施法进度展示。
    /// 按单位 SkillCasting（技能配置键）匹配 SkillsList。
    /// </summary>
    /// <returns>匹配的 Godot 技能资源；无施法返回 null。</returns>
    public UnitSkillBaseGodot? GetSpellingSkill() {
        string castingId = Unit.SkillCasting.Id;
        if (string.IsNullOrEmpty(castingId))
            return null;
        foreach (var skill in SkillsList) {
            if (skill.SkillId.Id == castingId)
                return skill;
        }
        return null;
    }

    /// <summary>
    /// 每帧从本地展示视图直读位置与朝向（XZ 平面）。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    override public void _Process(double delta) {
        var pos = Unit.Position;
        GlobalPosition = new Vector3(pos.X, 0f, pos.Y);

        var dir = Unit.Direction;
        if (dir.LengthSquared() > 0.0001f) {
            LookAt(GlobalPosition + new Vector3(dir.X, 0f, dir.Y));
        }
    }
}
