using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DungeonChessBattle.Battle.Entities;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 以独立子进程方式运行游戏服务器（<c>DungeonChessBattle.Server</c>）。
/// 采用查询式接口（无事件回调）：后台线程仅更新加锁保护的内部状态字段，
/// UI 在主线程轮询 <see cref="Status"/>，从根上避免跨线程触碰 Godot 节点。
/// 端口通过命令行参数 <c>--port</c> 传入；密码通过环境变量
/// <see cref="ServerProcessEnv.Password"/> 传入（避免密码暴露在进程命令行）。
/// </summary>
public sealed class ServerProcessHost : IServerHost {
    /// <summary>终止子进程等待超时。</summary>
    private static readonly TimeSpan KillWaitTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<ServerProcessHost> _logger;
    private readonly ServerProcessConfig _config;
    private readonly Func<int, bool> _readyProbe;
    private readonly Lock _lock = new();

    private Process? _process;
    private int _startPort;
    private int _port;
    private ServerHostStatus _status = ServerHostStatus.Stopped;
    private string? _lastError;
    private bool _stopRequested;
    private CancellationTokenSource? _cts;

    /// <inheritdoc cref="IServerHost.Status"/>
    public ServerHostStatus Status {
        get {
            lock (_lock)
                return _status;
        }
    }

    /// <inheritdoc cref="IServerHost.IsRunning"/>
    public bool IsRunning {
        get {
            lock (_lock)
                return _status != ServerHostStatus.Stopped;
        }
    }

    /// <inheritdoc cref="IServerHost.Port"/>
    public int Port {
        get {
            lock (_lock)
                return _port;
        }
    }

    /// <inheritdoc cref="IServerHost.LastError"/>
    public string? LastError {
        get {
            lock (_lock)
                return _lastError;
        }
    }

    /// <summary>
    /// 初始化服务器子进程宿主。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="config">启动配置；为空时使用默认值。</param>
    /// <param name="readyProbe">就绪探测委托；为空时使用 TCP 端口探测。便于单元测试注入。</param>
    public ServerProcessHost(ILogger<ServerProcessHost> logger, ServerProcessConfig? config = null, Func<int, bool>? readyProbe = null) {
        _logger = logger;
        _config = config ?? new ServerProcessConfig();
        _readyProbe = readyProbe ?? TryConnectPort;

        // Godot 进程退出/卸载时清理子进程，避免孤儿进程常驻。
        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillChildBestEffort();
        AppDomain.CurrentDomain.DomainUnload += (_, _) => KillChildBestEffort();
    }

    /// <inheritdoc cref="IServerHost.Start"/>
    public void Start(int port, string? serverPassword = null) {
        Process? process = null;
        CancellationTokenSource? cts = null;
        string? error = null;

        lock (_lock) {
            if (_process is not null) {
                _logger.LogWarning("服务器已在运行中");
                return;
            }

            string exe = ResolveExecutablePath();
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) {
                error = $"服务器可执行文件不存在: {exe}。请先构建 DungeonChessBattle.Server 工程。";
                _logger.LogError("{Error}", error);
            }
            else {
                string workingDir = string.IsNullOrEmpty(_config.WorkingDirectory)
                    ? (Path.GetDirectoryName(exe) ?? string.Empty)
                    : _config.WorkingDirectory;

                var psi = new ProcessStartInfo {
                    UseShellExecute = false,
                    // 隐藏子进程控制台窗口（Server 为控制台 apphost / dotnet 启动 dll）。
                    // 仅当 UseShellExecute=false 时有效，且不影响 stdout/stderr 重定向。
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = workingDir,
                };

                bool isDll = exe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                if (isDll) {
                    psi.FileName = "dotnet";
                    psi.ArgumentList.Add(exe);
                }
                else {
                    psi.FileName = exe;
                }
                psi.ArgumentList.Add("--port");
                psi.ArgumentList.Add(port.ToString());
                if (!string.IsNullOrEmpty(serverPassword))
                    psi.Environment[ServerProcessEnv.Password] = serverPassword;
                if (!string.IsNullOrEmpty(_config.ModDirectory))
                    psi.Environment[ServerProcessEnv.ModDir] = _config.ModDirectory;
                // 注入父进程 PID，供服务器端 ParentProcessWatcher 检测客户端存活（防孤儿）
                psi.Environment[ServerProcessEnv.ParentPid] = System.Environment.ProcessId.ToString();

                process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                process.OutputDataReceived += (_, e) => ForwardLog(e.Data);
                process.ErrorDataReceived += (_, e) => ForwardLog(e.Data, isError: true);
                process.Exited += OnProcessExited;

                try {
                    if (!process.Start()) {
                        error = "服务器进程启动失败（Start 返回 false）";
                        _logger.LogError("{Error}", error);
                        process.Dispose();
                        process = null;
                    }
                    else {
                        cts = new CancellationTokenSource();
                        _process = process;
                        _cts = cts;
                        _startPort = port;
                        _stopRequested = false;
                        _lastError = null;
                        SetStatusLocked(ServerHostStatus.Starting);
                    }
                }
                catch (Exception ex) {
                    error = $"服务器进程启动异常: {ex.Message}";
                    _logger.LogError(ex, "服务器进程启动异常");
                    process?.Dispose();
                    process = null;
                }
            }
        }

        if (process is null) {
            if (error is not null)
                SetLastError(error);
            return;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("服务器进程已启动: {Exe} port={Port}", process.StartInfo.FileName, port);
        _ = WaitForReadyAsync(process, port, cts?.Token ??
            throw new InvalidOperationException("服务器进程已启动但取消源缺失，时序错误。"));
    }

    /// <inheritdoc cref="IServerHost.Stop"/>
    public void Stop() {
        lock (_lock) {
            var process = _process;
            if (process is null) {
                _logger.LogWarning("服务器未在运行");
                return;
            }
            _stopRequested = true;
            _cts?.Cancel();
            KillProcess(process);
            ClearProcessLocked();
            SetStatusLocked(ServerHostStatus.Stopped);
            _logger.LogInformation("服务器已停止");
        }
    }

    /// <summary>
    /// 子进程退出回调（线程池线程）。仅更新内部状态，不触碰任何 UI/事件。
    /// 主动停止（<see cref="Stop"/>）时不记"意外退出"日志，避免误导。
    /// </summary>
    private void OnProcessExited(object? sender, EventArgs e) {
        lock (_lock) {
            if (!ReferenceEquals(sender, _process))
                return;

            if (!_stopRequested && _status != ServerHostStatus.Stopped) {
                int exitCode = (sender as Process)?.ExitCode ?? -1;
                _logger.LogWarning("服务器进程意外退出，退出码 {ExitCode}", exitCode);
                SetStatusLocked(ServerHostStatus.Stopped, "服务器进程异常退出");
            }
            ClearProcessLocked();
        }
    }

    /// <summary>
    /// 就绪探测循环：轮询探测端口直至成功（转为 <see cref="ServerHostStatus.Running"/>），
    /// 超时则终止进程并置为 <see cref="ServerHostStatus.Stopped"/>。
    /// </summary>
    private async Task WaitForReadyAsync(Process process, int port, CancellationToken ct) {
        var deadline = DateTime.UtcNow + _config.ReadyTimeout;
        try {
            while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline) {
                if (_readyProbe(port)) {
                    lock (_lock) {
                        if (ReferenceEquals(process, _process) && _status == ServerHostStatus.Starting) {
                            SetStatusLocked(ServerHostStatus.Running);
                            if (_logger.IsEnabled(LogLevel.Information))
                                _logger.LogInformation("服务器就绪，监听端口 {Port}", port);
                        }
                    }
                    return;
                }
                await Task.Delay(_config.ReadyPollInterval, ct);
            }

            if (!ct.IsCancellationRequested) {
                lock (_lock) {
                    if (ReferenceEquals(process, _process)) {
                        _logger.LogError("服务器就绪超时（{Timeout}s），正在终止进程", _config.ReadyTimeout.TotalSeconds);
                        KillProcess(process);
                        SetStatusLocked(ServerHostStatus.Stopped, $"服务器就绪超时（{_config.ReadyTimeout.TotalSeconds}s）");
                        ClearProcessLocked();
                    }
                }
            }
        }
        catch (OperationCanceledException) {
            // 主动停止：由 Stop 负责清理与状态
        }
    }

    /// <summary>默认就绪探测：尝试连接本机指定端口，判断服务器是否已开始监听。</summary>
    private static bool TryConnectPort(int port) {
        using var client = new TcpClient();
        try {
            client.Connect(IPAddress.Loopback, port);
            return client.Connected;
        }
        catch (SocketException) {
            return false;
        }
    }

    /// <summary>将子进程输出转发到 Godot 日志（错误输出走 Error 等级）。</summary>
    private void ForwardLog(string? line, bool isError = false) {
        if (string.IsNullOrWhiteSpace(line))
            return;
        if (isError)
            _logger.LogError("{Line}", line);
        else if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("{Line}", line);
    }

    /// <summary>终止子进程并等待其退出。</summary>
    private void KillProcess(Process process) {
        try {
            if (!process.HasExited)
                process.Kill();
            process.WaitForExit(KillWaitTimeout);
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "终止服务器进程异常");
        }
    }

    /// <summary>清理进程字段与取消源（调用方须持有锁）。</summary>
    private void ClearProcessLocked() {
        _cts?.Dispose();
        _cts = null;
        _process?.Dispose();
        _process = null;
        _port = 0;
    }

    /// <summary>
    /// 状态集中转移入口（调用方须持有锁）。集中在此保证一致性并记录日志。
    /// 失败原因仅在传入 <paramref name="error"/> 时更新；成功进入 Starting 时须先清空。
    /// </summary>
    /// <param name="next">目标状态。</param>
    /// <param name="error">可选失败原因，写入 <see cref="LastError"/>。</param>
    private void SetStatusLocked(ServerHostStatus next, string? error = null) {
        if (error is not null)
            _lastError = error;
        if (_status == next)
            return;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("服务器状态 {From} -> {To}", _status, next);
        _status = next;
        if (next == ServerHostStatus.Running)
            _port = _startPort;
        else if (next == ServerHostStatus.Stopped)
            _port = 0;
    }

    /// <summary>加锁更新最近错误（不改变运行状态）。</summary>
    private void SetLastError(string? error) {
        lock (_lock)
            _lastError = error;
    }

    /// <summary>Godot 退出时尽力清理子进程，避免孤儿进程。</summary>
    private void KillChildBestEffort() {
        Process? process;
        lock (_lock) {
            process = _process;
            _cts?.Cancel();
            _process = null;
        }
        if (process is null)
            return;
        try {
            if (!process.HasExited)
                process.Kill();
        }
        catch (Exception) {
            // 进程可能已退出，忽略
        }
        process.Dispose();
    }

    /// <summary>解析服务器可执行文件路径：相对 Godot 工程目录的 Server 工程 Debug 输出目录。</summary>
    private static string ResolveExecutablePath() {
        string projectDir = ProjectSettings.GlobalizePath("res://");
        string outDir = Path.Combine(projectDir, "..", "DungeonChessBattle.Server.Host", "bin", "Debug", "net10.0");
        string exe = Path.Combine(outDir, "DungeonChessBattle.Server.Host.exe");
        return File.Exists(exe) ? exe : Path.Combine(outDir, "DungeonChessBattle.Server.Host.dll");
    }
}
