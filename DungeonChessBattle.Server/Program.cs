using DungeonChessBattle.Server;
using DungeonChessBattle.Server.Settings;
using DungeonChessBattle.Server.Stores;
using Microsoft.Extensions.Logging;

// 从环境变量读取服务器密码（可选，为空表示无密码开发模式）
var config = ServerConfig.FromEnvironment();

using var loggerFactory = LoggerFactory.Create(builder => {
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// 装配大厅级状态存储（当前为进程内实现，可替换为其他 IGameStateStore 引擎）
using var stateStore = new InMemoryGameStateStore(loggerFactory);

var server = new GameServer(loggerFactory, config, stateStore);

// 注册 Ctrl+C 优雅退出
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    server.Stop();
    Environment.Exit(0);
};

// 阻塞式启动 + 内置 CLI
server.StartWithConsole();
