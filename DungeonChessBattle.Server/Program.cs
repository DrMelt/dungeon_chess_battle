using DungeonChessBattle.Server;

var server = new GameServer();
server.Start();

Console.WriteLine("DungeonChessBattle.Server is running. Press any key to stop...");
Console.ReadKey();

server.Stop();