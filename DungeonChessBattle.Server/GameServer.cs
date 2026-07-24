using System.Diagnostics;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Server.Handlers;
using DungeonChessBattle.Server.Network;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务端主控类，协调网络层和逻辑层。
/// 支持两种启动方式：
///   - StartAsync(): 非阻塞启动，供其他程序作为库调用，用 Stop() 关闭
///   - StartWithConsole(): 阻塞启动 + 内置 CLI，供独立控制台运行
/// </summary>
public class GameServer {
    private readonly ServerNetworkManager _networkManager;
    private readonly GameLogicService _logicService;
    private readonly GameMessageHandler _messageHandler;
    private Thread? _loopThread;

    private volatile bool _running;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private double _lastTickTime;

    /// <summary>
    /// 是否正在运行。
    /// </summary>
    public bool IsRunning => _running;

    private const double TickInterval = 0.05; // 20 Hz

    public GameServer() {
        _logicService = new GameLogicService();
        _networkManager = new ServerNetworkManager();
        _messageHandler = new GameMessageHandler(_networkManager);

        _networkManager.OnClientConnected += _messageHandler.OnClientConnected;
        _networkManager.OnClientDisconnected += _messageHandler.OnClientDisconnected;
        _networkManager.OnMessageReceived += _messageHandler.HandleMessage;
    }

    /// <summary>
    /// 非阻塞启动：开启网络监听，启动后台 Tick 线程后立即返回。
    /// 调用 Stop() 关闭。
    /// </summary>
    public void StartAsync() {
        if (_running)
            return;

        _networkManager.Start();
        _running = true;
        _lastTickTime = _tickWatch.Elapsed.TotalSeconds;

        _loopThread = new Thread(RunLoop) {
            Name = "GameServer-MainLoop",
            IsBackground = true
        };
        _loopThread.Start();

        Console.WriteLine("[GameServer] Started (async mode)");
    }

    /// <summary>
    /// 阻塞启动：开启网络 + 后台 Tick + 控制台 CLI 交互循环。
    /// 按 Ctrl+C 或输入 "exit" 即自动关闭。供独立控制台程序使用。
    /// </summary>
    public void StartWithConsole() {
        if (_running)
            return;

        StartAsync();

        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  DungeonChessBattle Server Console");
        Console.WriteLine("  Type 'help' for commands.");
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine();

        RunConsoleLoop();

        // CLI 退出后自动调用 Stop
        Stop();
    }

    /// <summary>
    /// 停止服务端：关闭网络，停止主循环线程。
    /// </summary>
    public void Stop() {
        _running = false;

        // 等待后台线程退出
        _loopThread?.Join(TimeSpan.FromSeconds(3));

        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine($"  Server stopped. Connections: {_networkManager.PeerCount}");
        Console.WriteLine("══════════════════════════════════════════");
        _networkManager.Stop();
    }

    private void RunLoop() {
        while (_running) {
            double now = _tickWatch.Elapsed.TotalSeconds;
            double deltaTime = now - _lastTickTime;

            _networkManager.PollEvents();

            if (deltaTime >= TickInterval) {
                _lastTickTime = now;
                Tick(deltaTime);
                if (now - _lastTickTime > TickInterval * 2) {
                    _lastTickTime = now;
                }
            }

            Thread.Sleep(1);
        }
    }

    private void Tick(double deltaTime) {
        // 遍历所有房间，更新战斗状态
    }

    #region Console CLI

    private void RunConsoleLoop() {
        while (true) {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line == null) // Ctrl+C 或流关闭
                break;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLowerInvariant();

            switch (cmd) {
                case "help":
                    Console.WriteLine("Available commands:");
                    Console.WriteLine("  status  - Show server status");
                    Console.WriteLine("  rooms   - List all rooms");
                    Console.WriteLine("  create <id> - Create a new room");
                    Console.WriteLine("  remove <id> - Remove a room");
                    Console.WriteLine("  exit    - Shutdown the server");
                    Console.WriteLine("  help    - Show this help");
                    break;

                case "status":
                    Console.WriteLine($"  Uptime: {_tickWatch.Elapsed:hh\\:mm\\:ss}");
                    Console.WriteLine($"  Connected clients: {_networkManager.PeerCount}");
                    break;

                case "rooms":
                    ListRooms();
                    break;

                case "create":
                    if (parts.Length < 2) {
                        Console.WriteLine("  Usage: create <roomId>");
                        break;
                    }
                    var room = _logicService.CreateRoom(parts[1]);
                    Console.WriteLine($"  Room '{room.RoomId}' created.");
                    break;

                case "remove":
                    if (parts.Length < 2) {
                        Console.WriteLine("  Usage: remove <roomId>");
                        break;
                    }
                    bool removed = _logicService.RemoveRoom(parts[1]);
                    Console.WriteLine(removed
                        ? $"  Room '{parts[1]}' removed."
                        : $"  Room '{parts[1]}' not found.");
                    break;

                case "exit":
                case "quit":
                    return;

                default:
                    Console.WriteLine($"  Unknown command: {cmd}. Type 'help' for commands.");
                    break;
            }
        }
    }

    private void ListRooms() {
        var allRooms = _logicService.GetAllRooms().ToList();
        if (allRooms.Count == 0) {
            Console.WriteLine("  No active rooms.");
            return;
        }

        foreach (var r in allRooms) {
            Console.WriteLine($"  Room: {r.RoomId} | UnitsA: {r.UnitsA.Count} | UnitsB: {r.UnitsB.Count} | Active: {r.IsActive}");
        }
    }

    #endregion
}