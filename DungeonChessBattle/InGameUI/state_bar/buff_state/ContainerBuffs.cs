using DungeonChessBattle.Entities;
using DungeonChessBattle.InGameUI.ui_interface;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// Buff 图标容器，根据单位 Pawn 同步的 Buff 列表动态创建/清理 Buff 图标。
/// 数据源为 Pawn.BuffsList（SyncBuffData，服务端权威）。
/// </summary>
public partial class ContainerBuffs : Control, IUIUpdate {
    /// <summary>导出引用集合节点。</summary>
    public ContainerBuffsInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<ContainerBuffsInterRefs>("ContainerBuffsInterRefs");
    }

    /// <summary>
    /// 根据单位 Pawn 刷新 Buff 图标列表。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    public void UpdateUI_WithUnit(UnitPawn pawn) {
        var buffContainer = InterRefs?.BuffContainer;
        if (buffContainer != null) {
            foreach (var child in buffContainer.GetChildren()) {
                child.QueueFree();
            }
        }

        if (pawn == null) {
            return;
        }
        if (InterRefs?.BuffIconPackedScene == null) {
            return;
        }

        // 为每个同步 Buff 数据创建图标占位（图标资源按 BuffTypeId 匹配待资源表补充）
        foreach (var buffData in pawn.BuffsList) {
            TextureRectBuffIcon buffIcon = InterRefs.BuffIconPackedScene.Instantiate<TextureRectBuffIcon>();
            buffIcon.SetBuffIcon(buffData, pawn);
            buffContainer?.AddChild(buffIcon);
        }
    }
}
