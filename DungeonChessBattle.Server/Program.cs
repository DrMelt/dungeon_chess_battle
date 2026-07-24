using DungeonChessBattle.Server;

var server = new GameServer();

// 注册 Ctrl+C 优雅退出
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    server.Stop();
    Environment.Exit(0);
};

// 阻塞式启动 + 内置 CLI
server.StartWithConsole();
