using System;
using DungeonChessBattle.MainScene;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 地牢计时标签，每帧由战斗开始时刻计算并显示战斗时长。
/// </summary>
public partial class DungeonTimeLabel : Label {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<DungeonTimeLabel> _logger = ServiceLocator.GetLogger<DungeonTimeLabel>();

    /// <summary>战斗会话上下文引用，用于获取战斗开始时刻。</summary>
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
    /// 每帧刷新显示当前战斗时间（从战斗开始时刻起经过的秒数，分:秒格式）。
    /// 开始时刻未同步（0 或 null）时显示占位，避免以 Unix 纪元为基准的错误计时。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        long? startUnix = _sessionRef?.BattleStartUnixTime;
        if (startUnix is not { } start || start <= 0) {
            Text = "Time: --:--";
            return;
        }
        long totalSeconds = (long)(DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(start)).TotalSeconds;
        Text = $"Time: {totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
    }

}

