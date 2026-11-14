using DungeonChessBattle.Game.BattleScene;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 地牢计时标签，每帧由统一数据源的战斗已运行秒数显示战斗时长。
/// 在线读数由本地时钟相对权威开始时刻推算，回放取引擎帧轴。
/// </summary>
public partial class DungeonTimeLabel : Label {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<DungeonTimeLabel> _logger = ServiceLocator.GetLogger<DungeonTimeLabel>();

    /// <summary>战斗会话上下文引用，用于获取战斗计时。</summary>
    [Export]
    private BattleSessionContext? _sessionRef;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (_sessionRef == null)
            _logger.LogError("_sessionRef is not assigned!");
    }

    /// <summary>
    /// 每帧刷新显示战斗已运行秒数（分:秒格式）。计时未就绪时显示占位。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (_sessionRef?.BattleElapsed is not { } elapsed || elapsed <= 0) {
            Text = "Time: --:--";
            return;
        }
        long totalSeconds = (long)elapsed;
        Text = $"Time: {totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
    }
}

