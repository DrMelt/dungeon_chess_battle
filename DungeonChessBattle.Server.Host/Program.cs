using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Server.Host;

// 命令行参数 --port 指定大厅监听端口，默认见 ServerConfig.DefaultPort。
int port = ServerConfig.DefaultPort;
string? portArg = GetArg("--port");
if (int.TryParse(portArg, out int parsedPort) && parsedPort is > 0 and <= 65535)
    port = parsedPort;

// mods 根目录：命令行 --mod-dir 优先，回退环境变量（客户端子进程注入）。
string? modDir = GetArg("--mod-dir")
    ?? Environment.GetEnvironmentVariable(ServerProcessEnv.ModDir);

// 从环境变量读取服务器密码，可选，为空表示无密码开发模式
bool hasServerPassword = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ServerProcessEnv.Password));

using var loggerFactory = LoggerFactory.Create(builder => {
    builder.AddSimpleConsole(options => {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss.fff ";
    });
    builder.SetMinimumLevel(LogLevel.Debug);
});

// 让 LES 网络框架日志进入统一日志体系 Console，并早于任何 EntityManager 创建
LesNetworkLogger.Install(loggerFactory.CreateLogger(nameof(LiteEntitySystem)));

// 装配 mod 内容：代码 mod 行为注册 → 数据合并 → 注册表重建，必须在任何房间创建前完成。
var boot = ContentBootstrapper.Load(modDir);
var logger = loggerFactory.CreateLogger("Mod");
foreach (var error in boot.Errors)
    logger.LogError("mod 装载失败: {Error}", error);
if (logger.IsEnabled(LogLevel.Information))
    logger.LogInformation("内容装配完成：mods={Count} fingerprint={Fingerprint}", boot.Mods.Count, boot.Fingerprint);
Console.WriteLine($"  Content fingerprint: {boot.Fingerprint}");

// ASP.NET Core Kestrel 与 SignalR 大厅服务宿主
var host = new GameServerHost(
    loggerFactory.CreateLogger<GameServerHost>(),
    loggerFactory);

// 父进程看护：客户端即父进程退出、强杀或崩溃时自动停止服务器，避免孤儿进程。
// 未配置父 PID 即独立运行时返回 null 不启用。
ParentProcessWatcher.FromEnvironment(host, loggerFactory.CreateLogger<ParentProcessWatcher>())?.Start();

// 注册 Ctrl+C 优雅退出
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    host.Stop();
    Environment.Exit(0);
};

host.Start(port);

Console.WriteLine("══════════════════════════════════════════");
Console.WriteLine("  DungeonChessBattle Server (ASP.NET Core + SignalR)");
Console.WriteLine($"  Lobby port: {port}");
Console.WriteLine($"  Server password: {(hasServerPassword ? "ENABLED" : "DISABLED")}");
Console.WriteLine("  Press Ctrl+C to stop.");
Console.WriteLine("══════════════════════════════════════════");

// 阻塞式运行直至停止
Thread.Sleep(Timeout.Infinite);

// 读取命令行参数：--port <value>
static string? GetArg(string name) {
    string[] args = Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length - 1; i++) {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }
    return null;
}
