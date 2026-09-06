using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Server.Host;

// 进程级日志与 ASP.NET Core 宿主日志共用同一套配置
using var loggerFactory = LoggerFactory.Create(builder => builder.ConfigureConsole());
var logger = loggerFactory.CreateLogger("Host");

// 解析命令行参数与环境变量为唯一装配配置：--port、--mod-dir，环境变量见 ServerProcessEnv。
ServerConfig config = ServerConfig.Load(args);
// 无效父 PID 环境变量按独立运行处理，保留告警避免静默
if (Environment.GetEnvironmentVariable(ServerProcessEnv.ParentPid) is { Length: > 0 } rawPid
    && (!int.TryParse(rawPid, out int parsedPid) || parsedPid <= 0))
    logger.LogWarning("父 PID 环境变量无效: {Value}，按独立运行模式启动。", rawPid);

// 让 LES 网络框架日志进入统一日志体系 Console，并早于任何 EntityManager 创建
LesNetworkLogger.Install(loggerFactory.CreateLogger(nameof(LiteEntitySystem)));

// 装配 mod 内容：代码 mod 行为注册 → 数据合并 → 注册表重建，必须在任何房间创建前完成。
var boot = ContentBootstrapper.Load(config.ModDir);
var modLogger = loggerFactory.CreateLogger("Mod");
foreach (var error in boot.Errors)
    modLogger.LogError("mod 装载失败: {Error}", error);
if (modLogger.IsEnabled(LogLevel.Information))
    modLogger.LogInformation("内容装配完成：mods={Count} fingerprint={Fingerprint}", boot.Mods.Count, boot.Fingerprint);
Console.WriteLine($"  Content fingerprint: {boot.Fingerprint}");

// 配置了父进程但已不存在：客户端已死，服务器不应继续启动
if (ParentProcessWatcher.IsParentGone(config)) {
    logger.LogWarning("父进程 {Pid} 已不存在，服务器退出。", config.ParentPid);
    return 0;
}

// ASP.NET Core Kestrel 与 SignalR 大厅服务宿主
var host = new GameServerHost(loggerFactory, config);
if (!host.Start())
    return 1;

// 父进程看护：客户端即父进程退出、强杀或崩溃时自动停止服务器，避免孤儿进程。
// 置于宿主启动后装配：父进程消失经 host.Stop 走统一的优雅收尾路径。
ParentProcessWatcher.Create(config, host, loggerFactory.CreateLogger<ParentProcessWatcher>())?.Start();

// Ctrl+C/SIGTERM 由宿主 ConsoleLifetime 转为停止信号，无需手写注册

Console.WriteLine("══════════════════════════════════════════");
Console.WriteLine("  DungeonChessBattle Server (ASP.NET Core + SignalR)");
Console.WriteLine($"  Lobby port: {config.LobbyPort}");
Console.WriteLine($"  Server password: {(string.IsNullOrEmpty(config.ServerPassword) ? "DISABLED" : "ENABLED")}");
Console.WriteLine("  Press Ctrl+C to stop.");
Console.WriteLine("══════════════════════════════════════════");

// 阻塞等待停止信号，完成后宿主优雅收尾并自然退出
await host.RunAsync();
return 0;
