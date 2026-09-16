using System;
using DungeonChessBattle.Client;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Config.Registry;
using DungeonChessBattle.Game.Mod.Manager;
using DungeonChessBattle.Replay.Client;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 服务定位器：持有服务与两次装配产物的单例，服务为 ServerService、ClientService 与 ReplayService，
/// 装配产物为数据面 <see cref="GameContent"/> 与展示面 <see cref="ModAssets"/>。
/// ReplayService 内部组合 ReplayClient（获取）与 ReplayCache（缓存）。
/// 创建 ILoggerFactory（Console + Godot Provider），注入 Logger 到各 Service。
/// </summary>
public static class ServiceLocator {
    /// <summary>日志工厂实例（Godot 控制台 Provider）。此处限定框架类型名：本类的 LoggerFactory 属性与它同名。</summary>
    private static readonly ILoggerFactory LoggerFactoryInstance =
        Microsoft.Extensions.Logging.LoggerFactory.Create(builder => {
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
    /// 日志工厂，供装配流程把日志通道注入下层库；下层的类别名即其自身类型。
    /// </summary>
    public static ILoggerFactory LoggerFactory => LoggerFactoryInstance;

    /// <summary>
    /// 通过字符串类别名创建 ILogger。供基类等无法确定具体类型时使用。
    /// </summary>
    public static ILogger CreateLogger(string categoryName) => LoggerFactoryInstance.CreateLogger(categoryName);

    /// <summary>游戏服务器宿主单例（独立子进程实现），注入 mods 根目录使子进程加载同一启用内容。</summary>
    public static readonly IServerHost ServerService = new ServerProcessHost(
        LoggerFactoryInstance.CreateLogger<ServerProcessHost>(),
        new ServerProcessConfig {
            ModDirectory = ModManager.ModsRootPath,
        });

    private static GameClientService? _clientService;

    /// <summary>
    /// 游戏客户端服务单例。惰性创建：房间客户端要按内容目录装配领域单位与副本布局，
    /// 而内容在 `MainScene._EnterTree` 才装配，静态初始化期内拿不到。
    /// </summary>
    public static GameClientService ClientService => _clientService ??= new GameClientService(
        LoggerFactoryInstance,
        new DefaultClientConnectionFactory(GameContent.Registry));

    /// <summary>
    /// mod 展示装配产物，UI 与表现组件的展示取数入口；装配前为 null，取用方按未注册处理。
    /// 值由 <c>ModManager.EnsureInitialized</c> 写入：它产自一次有先后次序的装配，
    /// 不能在静态初始化期内就地构造，故不像其余服务那样用 readonly 内联字段。
    /// </summary>
    public static ModAssets? ModAssets {
        get; internal set;
    }

    private static GameContent? _gameContent;

    /// <summary>
    /// 数据面装配产物：本次装配的内容注册表与单位目录，UI 与回放门控取数经它。
    /// 值由 <c>ModManager.EnsureInitialized</c> 写入，写入前取用即抛，不静默给空内容。
    /// </summary>
    public static GameContent GameContent => _gameContent
        ?? throw new InvalidOperationException("内容尚未装配：ModManager.EnsureInitialized 未执行。");

    /// <summary>写入数据面装配产物，仅装配流程调用。</summary>
    internal static void BindContent(GameContent content) => _gameContent = content;

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
        GameContent.Registry,
        LoggerFactoryInstance.CreateLogger<ReplayService>());
}
