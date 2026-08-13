using System;
using DungeonChessBattle.Battle.Domain.Enums;
using Godot;

namespace DungeonChessBattle.Effects;

/// <summary>
/// 目标标记节点：用于在棋盘上显示单位的目标圈标记，并根据阵营关系着色。
/// </summary>
public partial class Node3dTargetMark : Node3D {
    /// <summary>导出引用集合节点。</summary>
    public Node3dTargetMarkInterRefs? InterRefs {
        get; private set;
    }

    private Node3dTargetMarkInterRefs InterRefsOrThrow =>
        InterRefs ?? throw new InvalidOperationException("[Node3dTargetMark] InterRefs has not been initialized.");

    /// <summary>目标标记贴花引用。</summary>
    public Decal? TargetDecalRef => InterRefsOrThrow.TargetDecalRef;

    /// <summary>
    /// 节点就绪时初始化引用，并应用默认颜色。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<Node3dTargetMarkInterRefs>("Node3dTargetMarkInterRefs");
        SetColor(CampRelation.Neutral);
    }

    /// <summary>
    /// 设置目标标记半径，仅修改节点缩放。
    /// </summary>
    /// <param name="radius">半径。</param>
    public void SetRadius(float radius) {
        Scale = new Vector3(radius, 1, radius);
    }

    /// <summary>
    /// 根据阵营关系设置目标标记颜色。
    /// </summary>
    /// <param name="relation">阵营关系：友方、中立、敌方。</param>
    public void SetColor(CampRelation relation) {
        var interRefs = InterRefsOrThrow;
        var uiSettings = interRefs.PlayerUISettingsRes
            ?? throw new InvalidOperationException("[Node3dTargetMark] PlayerUISettingsRes is not assigned.");
        var targetDecal = interRefs.TargetDecalRef
            ?? throw new InvalidOperationException("[Node3dTargetMark] TargetDecalRef is not assigned.");

        targetDecal.Modulate = relation switch {
            CampRelation.Friendly => uiSettings.AllyCampColor,
            CampRelation.Enemy => uiSettings.EnemyCampColor,
            _ => uiSettings.NeutralCampColor,
        };
    }

    /// <summary>
    /// 根据阵营名称设置目标标记颜色，为空时使用中立颜色。
    /// </summary>
    /// <param name="camp">阵营名称。</param>
    public void SetCampColor(string camp) {
        SetColor(GetRelation(camp));
    }

    private static CampRelation GetRelation(string camp) => camp switch {
        CampConstants.CampA => CampRelation.Friendly,
        CampConstants.CampB => CampRelation.Enemy,
        CampConstants.CampBoss => CampRelation.Neutral,
        "" => CampRelation.Neutral,
        _ => CampRelation.Friendly,
    };

}
