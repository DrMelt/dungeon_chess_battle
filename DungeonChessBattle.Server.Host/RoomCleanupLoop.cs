using DungeonChessBattle.Server.Abstractions;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 空房间清理循环：周期消费 <see cref="IBattleRoomManager.ProcessPendingRoomCleanups"/>。
/// 线程所有权由宿主显式持有，停止时序由宿主保证：先停循环再停全部房间。
/// </summary>
/// <param name="roomManager">战斗房间生命周期协调器。</param>
/// <param name="logger">日志记录器。</param>
/// <param name="interval">清理周期。</param>
public sealed class RoomCleanupLoop(IBattleRoomManager roomManager, ILogger<RoomCleanupLoop> logger,
    TimeSpan? interval = null) {
    private readonly TimeSpan _interval = interval ?? TimeSpan.FromMilliseconds(50);
    private readonly Lock _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    /// <summary>启动清理循环，幂等。</summary>
    public void Start() {
        lock (_lock) {
            if (_cts != null)
                return;
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => LoopAsync(_cts.Token), _cts.Token);
        }
    }

    /// <summary>取消循环并等待其退出，幂等。</summary>
    public void Stop(TimeSpan timeout) {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_lock) {
            cts = _cts;
            loop = _loopTask;
            _cts = null;
            _loopTask = null;
        }
        if (cts == null)
            return;

        cts.Cancel();
        try {
            // _cts.Token 此刻已取消，等待无需取消
            if (loop != null && !loop.Wait(timeout, CancellationToken.None)
                && logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("空房间清理循环未在超时内停止");
        }
        catch (AggregateException) {
            // 循环已因清理失败终止，此处观测异常避免 UnobservedTaskException
        }
        cts.Dispose();
    }

    private async Task LoopAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(_interval);
        try {
            while (await timer.WaitForNextTickAsync(ct)) {
                try {
                    roomManager.ProcessPendingRoomCleanups();
                }
                catch (Exception ex) {
                    if (logger.IsEnabled(LogLevel.Error))
                        logger.LogError(ex, "空房间清理失败，循环继续");
                }
            }
        }
        catch (OperationCanceledException) {
            // 正常停止
        }
    }
}
