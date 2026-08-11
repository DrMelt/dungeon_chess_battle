using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 父进程看护：检测宿主客户端进程即父进程是否存活，父进程消失时触发服务器优雅退出。
/// 解决问题：客户端被强杀或崩溃时，仅靠其托管事件 AppDomain.ProcessExit 无法清理子进程，
/// 导致服务器成为孤儿进程继续运行。
/// 采用独立组件：探测函数与退出动作均为注入点，便于单元测试与替换宿主实现。
/// 未配置父 PID，如服务器独立手动运行，时不启用，不影响正常启动。
/// </summary>
public sealed class ParentProcessWatcher {
    /// <summary>父进程 PID 传输环境变量名，由客户端 ServerProcessHost 写入，跨进程契约。</summary>
    public const string ParentPidEnvVar = "DCB_SERVER_PARENT_PID";

    /// <summary>默认看护间隔。</summary>
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
    /// 从环境变量装配看护器。
    /// 未配置父 PID 返回 null，独立运行模式；配置无效则容忍并按独立运行处理。
    /// 配置了父 PID 但父进程已不存在 → 服务器不应继续运行，直接优雅退出并返回 null。
    /// </summary>
    /// <param name="host">服务器宿主，优雅退出动作的目标。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="interval">看护间隔；为空使用默认值。</param>
    public static ParentProcessWatcher? FromEnvironment(GameServerHost host, ILogger logger,
        TimeSpan? interval = null) {
        string? pidStr = Environment.GetEnvironmentVariable(ParentPidEnvVar);
        if (string.IsNullOrEmpty(pidStr))
            return null;

        if (!int.TryParse(pidStr, out int parentPid) || parentPid <= 0) {
            logger.LogWarning("父 PID 环境变量无效: {Value}，按独立运行模式启动。", pidStr);
            return null;
        }

        DateTime? startTime = TryGetStartTime(parentPid);
        if (startTime == null) {
            // 配置了父 PID 但父进程已消失：服务器继续运行将成孤儿，直接优雅退出
            logger.LogWarning("父进程 {Pid} 已不存在，服务器自动退出。", parentPid);
            host.Stop();
            Environment.Exit(0);
            return null;
        }

        return new ParentProcessWatcher(parentPid, startTime.Value, interval ?? DefaultInterval,
            TryGetStartTime, () => {
                host.Stop();
                Environment.Exit(0);
            }, logger);
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
