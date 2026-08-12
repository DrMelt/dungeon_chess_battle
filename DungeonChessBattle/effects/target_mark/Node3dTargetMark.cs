using System;
using DungeonChessBattle.Entities;
using DungeonChessBattle.GamePlayUI.Interfaces;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 目标标记节点：用于在棋盘上显示单位的目标圈标记，并根据阵营着色。
/// </summary>
public partial class Node3dTargetMark : Node3D, IUIUpdate {
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
        SetCampColor("");
    }

    /// <summary>
    /// 根据阵营名称设置目标标记颜色。
    /// </summary>
    /// <param name="camp">阵营名称，为空时使用默认颜色。</param>
    public void SetCampColor(string camp) {
        var interRefs = InterRefsOrThrow;
        var uiSettings = interRefs.PlayerUISettingsRes
            ?? throw new InvalidOperationException("[Node3dTargetMark] PlayerUISettingsRes is not assigned.");
        var targetDecal = interRefs.TargetDecalRef
            ?? throw new InvalidOperationException("[Node3dTargetMark] TargetDecalRef is not assigned.");
        Color? resColor = uiSettings.GetCampColor(camp);

        resColor ??= interRefs.DefultColor;

        targetDecal.Modulate = (Color)resColor;
    }

    /// <summary>
    /// 根据单位 Pawn 更新目标标记：聚焦单位时显示其阵营颜色，否则使用默认颜色，并同步标记大小。
    /// </summary>
    /// <param name="pawn">单位 Pawn。</param>
    public void UpdateUI_WithUnit(UnitPawn pawn) {
        var interRefs = InterRefsOrThrow;
        var uiRes = interRefs.PlayerInterfaceRes
            ?? throw new InvalidOperationException("[Node3dTargetMark] PlayerInterfaceRes is not assigned.");
        if (uiRes.FocusOnUnit != null && pawn == uiRes.FocusOnUnit.Pawn) {
            SetCampColor(pawn.Camp.Value);
        }
        else {
            SetCampColor("");
        }

        Scale = new Vector3(pawn.BodyRadius.Value, 1, pawn.BodyRadius.Value);
    }

    /// <summary>
    /// 将目标标记恢复为默认外观。
    /// </summary>
    public void SetMark_Normal() {
        SetCampColor("");
    }

    /// <summary>
    /// 将目标标记设为聚焦状态，使用对应单位的阵营颜色。
    /// </summary>
    /// <param name="unitShow">被聚焦的单位显示对象。</param>
    internal void SetMark_Focus(UnitGameShow unitShow) {
        SetCampColor(unitShow.Pawn.Camp.Value);
    }
}
