using DungeonChessBattle.Entities;
using DungeonChessBattle.Server.Host;
using Microsoft.Extensions.Logging;

// 命令行参数 --port 指定大厅监听端口（默认见 ServerConfig.DefaultPort）。
int port = ServerConfig.DefaultPort;
string? portArg = GetArg("--port");
if (int.TryParse(portArg, out int parsedPort) && parsedPort is > 0 and <= 65535)
    port = parsedPort;

// 从环境变量读取服务器密码（可选，为空表示无密码开发模式）
bool hasServerPassword = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DCB_SERVER_PASSWORD"));

using var loggerFactory = LoggerFactory.Create(builder => {
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// 让 LES 网络框架日志进入统一日志体系（Console），并早于任何 EntityManager 创建
LesNetworkLogger.Install(loggerFactory.CreateLogger(nameof(LiteEntitySystem)));

// ASP.NET Core (Kestrel + SignalR) 大厅服务宿主
var host = new GameServerHost(
    loggerFactory.CreateLogger<GameServerHost>(),
    loggerFactory);

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
