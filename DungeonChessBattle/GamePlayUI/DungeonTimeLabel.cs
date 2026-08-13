using DungeonChessBattle.MainScene;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 地牢计时标签，定期从场景单位集合读取战斗时间并显示。
/// </summary>
public partial class DungeonTimeLabel : Label {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<DungeonTimeLabel> _logger = ServiceLocator.GetLogger<DungeonTimeLabel>();

    /// <summary>战斗单位管理器引用，用于获取当前战斗时间。</summary>
    [Export]
    private BattleUnitManager? unitsInSceneRef;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (unitsInSceneRef == null)
            _logger.LogError("unitsInSceneRef is not assigned!");
    }

    /// <summary>
    /// 每帧刷新显示当前战斗时间（从房间创建时刻起经过的秒数，分:秒格式）。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (unitsInSceneRef == null)
            return;

        var totalSeconds = (int)unitsInSceneRef.UnitsInSceneRes.SceneTime;
        Text = $"Time: {totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
    }

}
