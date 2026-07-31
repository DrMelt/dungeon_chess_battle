using DungeonChessBattle.Server;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => {
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var server = new GameServer(loggerFactory);

// 注册 Ctrl+C 优雅退出
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    server.Stop();
    Environment.Exit(0);
};

// 阻塞式启动 + 内置 CLI
server.StartWithConsole();
