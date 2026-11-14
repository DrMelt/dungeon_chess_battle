using System.Diagnostics;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 父进程看护：检测宿主客户端进程即父进程是否存活，父进程消失时触发服务器优雅退出。
/// 解决问题：客户端被强杀或崩溃时，仅靠其托管事件 AppDomain.ProcessExit 无法清理子进程，
/// 导致服务器成为孤儿进程继续运行。
/// 采用独立组件：探测函数与退出动作均为注入点，便于单元测试与替换宿主实现。
/// 未配置父 PID，即服务器独立手动运行时，不启用。
/// </summary>
public sealed class ParentProcessWatcher {
    /// <summary>父进程 PID 探测周期。</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(1);

    private readonly Func<int, DateTime?> _getStartTime;
    private readonly Action _onParentGone;
    private readonly int _parentPid;
    private readonly DateTime _parentStartTime;
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private Thread? _thread;

    private ParentProcessWatcher(int parentPid, DateTime parentStartTime, TimeSpan interval,
        Func<int, DateTime?> getStartTime, Action onParentGone, ILogger logger) {
        _parentPid = parentPid;
        _parentStartTime = parentStartTime;
        _interval = interval;
        _getStartTime = getStartTime;
        _onParentGone = onParentGone;
        _logger = logger;
    }

    /// <summary>
    /// 预检父进程是否早已消失。配置了父 PID 且进程已不存在时返回 true，
    /// 服务器不应继续启动；由入口在装配宿主前调用。
    /// </summary>
    public static bool IsParentGone(ServerConfig config) =>
        config.ParentPid is { } parentPid && TryGetStartTime(parentPid) == null;

    /// <summary>
    /// 从配置装配看护器。未配置父 PID 返回 null，独立运行模式。
    /// 装配应在宿主启动后执行；发现父进程已消失时发出停止信号，收尾归入口 RunAsync。
    /// </summary>
    /// <param name="config">服务器装配配置。</param>
    /// <param name="host">服务器宿主，优雅退出动作的目标。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="interval">看护间隔；为空使用默认值。</param>
    public static ParentProcessWatcher? Create(ServerConfig config, GameServerHost host, ILogger logger,
        TimeSpan? interval = null) {
        if (config.ParentPid is not { } parentPid)
            return null;

        DateTime? startTime = TryGetStartTime(parentPid);
        if (startTime == null) {
            // 竞态窗口：入口预检通过后宿主构建期间父进程消失，服务器不应继续运行
            logger.LogWarning("父进程 {Pid} 已不存在，服务器自动停止。", parentPid);
            host.Stop();
            return null;
        }

        // 看护期间父进程消失：发出停止信号，由入口 RunAsync 完成优雅收尾与进程退出
        return new ParentProcessWatcher(parentPid, startTime.Value, interval ?? DefaultInterval,
            TryGetStartTime, host.Stop, logger);
    }

    /// <summary>启动看护后台线程，幂等。</summary>
    public void Start() {
        lock (_lock) {
            if (_thread != null)
                return;
            _thread = new Thread(WatchLoop) {
                IsBackground = true,
                Name = "ParentProcessWatcher",
            };
            _thread.Start();
        }
    }

    /// <summary>
    /// 看护循环：周期探测父进程。父进程消失或 PID 被系统复用，启动时间不一致，时触发退出动作。
    /// </summary>
    private void WatchLoop() {
        while (true) {
            Thread.Sleep(_interval);
            DateTime? current = _getStartTime(_parentPid);
            if (current == null || current != _parentStartTime) {
                _logger.LogWarning("父进程 {Pid} 已退出，服务器自动停止。", _parentPid);
                _onParentGone();
                return;
            }
        }
    }

    /// <summary>探测指定进程的启动时间；进程不存在或访问失败返回 null，视为父进程已消失。</summary>
    private static DateTime? TryGetStartTime(int pid) {
        try {
            return Process.GetProcessById(pid).StartTime;
        }
        catch (Exception) {
            return null;
        }
    }
}
