using System;
using Godot;
using Godot.Collections;
using DungeonChessBattle.Entities;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 单位 3D 展示组件。
/// 持有网络同步 UnitPawn（LES SyncVar，服务端权威），每帧直读位置/朝向驱动网格。
/// 技能展示资源（UnitSkillBaseGodot）由 UnitShowManager 注入。
/// </summary>
public partial class UnitGameShow : Node3D {
    /// <summary>网络同步单位 Pawn（运行时注入，由 UnitShowManager.SpawnUnit 赋值）。</summary>
    private UnitPawn? _pawn;

    /// <summary>网络同步单位 Pawn。</summary>
    public UnitPawn Pawn {
        get => _pawn ?? throw new InvalidOperationException("Pawn has not been assigned.");
        set => _pawn = value;
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
    /// 按 Pawn.SkillCasting（技能配置键）匹配 SkillsList。
    /// </summary>
    /// <returns>匹配的 Godot 技能资源；无施法返回 null。</returns>
    public UnitSkillBaseGodot? GetSpellingSkill() {
        var castingId = Pawn.SkillCasting.Value;
        if (castingId == 0)
            return null;
        foreach (var skill in SkillsList) {
            if (skill.SkillId.Id == castingId)
                return skill;
        }
        return null;
    }

    /// <summary>
    /// 每帧从 Pawn 直读网络位置与朝向（XZ 平面 SyncVar）。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    override public void _Process(double delta) {
        var pos = Pawn.Position.InterpolatedValue;
        GlobalPosition = new Vector3(pos.X, 0f, pos.Y);

        var dir = Pawn.Direction.InterpolatedValue;
        if (dir.LengthSquared() > 0.0001f) {
            LookAt(GlobalPosition + new Vector3(dir.X, 0f, dir.Y));
        }
    }
}
