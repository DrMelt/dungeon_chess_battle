using Godot;
using DungeonChessBattle.InGameUI.ui_interface;

namespace DungeonChessBattle;

/// <summary>
/// Buff 图标容器，根据单位状态动态创建/清理 Buff 图标。
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
    /// 根据单位状态刷新 Buff 图标列表。
    /// </summary>
    /// <param name="unitState">目标单位状态。</param>
    public void UpdateUI_WithUnit(UnitState unitState) {
        var buffContainer = InterRefs?.BuffContainer;
        if (buffContainer != null) {
            foreach (var child in buffContainer.GetChildren()) {
                child.QueueFree();
            }
        }

        if (unitState == null) {
            return;
        }
        if (InterRefs?.BuffIconPackedScene == null) {
            return;
        }

        foreach (BuffBaseGodot buff in unitState.BuffList) {
            TextureRectBuffIcon buffIcon = InterRefs.BuffIconPackedScene.Instantiate<TextureRectBuffIcon>();
            buffIcon.SetBuffIcon(buff, unitState);
            buffContainer?.AddChild(buffIcon);
        }
    }
}
