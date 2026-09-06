using System;
using DungeonChessBattle.Client;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Replay.Client;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 服务定位器，持有 ServerService、ClientService 与 ReplayService 的单例。
/// ReplayService 内部组合 ReplayClient（获取）与 ReplayCache（缓存）。
/// 创建 ILoggerFactory（Console + Godot Provider），注入 Logger 到各 Service。
/// </summary>
public static class ServiceLocator {
    /// <summary>默认服务器端口。</summary>
    public const int DefaultPort = NetworkDefaults.LobbyPort;

    /// <summary>日志工厂实例（Godot 控制台 Provider）。</summary>
    private static readonly ILoggerFactory LoggerFactoryInstance = LoggerFactory.Create(builder => {
        builder.AddProvider(new GodotLoggerProvider());
        builder.SetMinimumLevel(LogLevel.Debug);
    });

    /// <summary>
    /// 静态构造函数：静态字段初始化完成后即可安全读取 LoggerFactoryInstance，
    /// 在此安装 LES 网络框架日志（Godot 控制台）。
    /// 独立 .NET 服务端进程则在 Program.cs 中单独安装（Console）。
    /// </summary>
    static ServiceLocator() {
        LiteEntitySystem.Logger.LoggerImpl = new LesNetworkLogger(
            LoggerFactoryInstance.CreateLogger(nameof(LiteEntitySystem)));
    }

    /// <summary>
    /// 获取指定类型的 ILogger 实例。供 Godot 端面板/实体使用，便于排查问题。
    /// </summary>
    public static ILogger<T> GetLogger<T>() => LoggerFactoryInstance.CreateLogger<T>();

    /// <summary>
    /// 通过字符串类别名创建 ILogger。供基类等无法确定具体类型时使用。
    /// </summary>
    public static ILogger CreateLogger(string categoryName) => LoggerFactoryInstance.CreateLogger(categoryName);

    /// <summary>游戏服务器宿主单例（独立子进程实现），注入 mods 根目录使子进程加载同一启用内容。</summary>
    public static readonly IServerHost ServerService = new ServerProcessHost(
        LoggerFactoryInstance.CreateLogger<ServerProcessHost>(),
        new ServerProcessConfig {
            ModDirectory = ProjectSettings.GlobalizePath(ModManager.ModsRootGodotPath),
        });

    /// <summary>游戏客户端服务单例。</summary>
    public static readonly GameClientService ClientService = new(
        LoggerFactoryInstance);

    private static ReplayService? _replayService;

    // Godot user:// 虚拟路径，非文件系统绝对路径，S1075 误报
#pragma warning disable S1075
    private const string ReplaysRootGodotPath = "user://replays";
#pragma warning restore S1075

    /// <summary>
    /// 回放浏览服务单例：托管会话状态与取数编排（获取、缓存、解码、门控、并集）。
    /// 惰性创建，构造时读 Godot 路径设置，避开静态初始化早于引擎就绪的问题。
    /// 服务器根地址与会话凭证都按需提供：前者取大厅端口（房间重定向不改它），
    /// 后者随登录换发，缓存下来就会用到已作废的凭证。
    /// </summary>
    public static ReplayService ReplayService => _replayService ??= new ReplayService(
        new ReplayClient(
            // 局域网回放服务，与大厅同宿主，不启用 TLS
#pragma warning disable S5332
            static () => new Uri($"http://{ClientService.Host}:{ClientService.LobbyPort}"),
#pragma warning restore S5332
            static () => ClientService.SessionToken,
            LoggerFactoryInstance.CreateLogger<ReplayClient>()),
        new ReplayCache(ProjectSettings.GlobalizePath(ReplaysRootGodotPath)),
        LoggerFactoryInstance.CreateLogger<ReplayService>());
}
