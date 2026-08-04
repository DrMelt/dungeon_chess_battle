using DungeonChessBattle.Server;
using Microsoft.Extensions.Logging;

// 从环境变量读取服务器密码（可选，为空表示无密码开发模式）
var serverPassword = Environment.GetEnvironmentVariable("DCB_SERVER_PASSWORD");

using var loggerFactory = LoggerFactory.Create(builder => {
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var server = new GameServer(loggerFactory, serverPassword);

// 注册 Ctrl+C 优雅退出
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    server.Stop();
    Environment.Exit(0);
};

// 阻塞式启动 + 内置 CLI
server.StartWithConsole();
