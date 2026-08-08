using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 地牢计时标签，定期从场景单位集合读取战斗时间并显示。
/// </summary>
public partial class DungeonTimeLabel : Label {
    /// <summary>战斗单位管理器引用，用于获取当前战斗时间。</summary>
    [Export]
    private BattleUnitManager? unitsInSceneViewRef;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (unitsInSceneViewRef == null)
            GD.PrintErr("[DungeonTimeLabel] [Export] unitsInSceneViewRef is not assigned!");
    }

    /// <summary>
    /// 每帧刷新显示当前战斗时间。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (unitsInSceneViewRef == null)
            return;
        Text = "Time: " + unitsInSceneViewRef.UnitsInSceneRes.SceneTime.ToString("F0");
    }

}
