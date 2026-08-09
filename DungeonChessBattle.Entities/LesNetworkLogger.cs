using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 将 LiteEntitySystem 框架静态日志（LiteEntitySystem.Logger.LoggerImpl）
/// 转发到 Microsoft.Extensions.Logging 体系（Godot/Console 由注入的 ILogger Provider 决定输出）。
/// 平台无关，仅做接口桥接，不重复实现任何平台输出。
/// 使用常量模板 + IsEnabled 预检，规避 CA2254（模板不一致）与 CA1873（禁用时求值参数）。
/// </summary>
public sealed class LesNetworkLogger(ILogger logger) : LiteEntitySystem.ILogger {
    private readonly ILogger _logger = logger;

    /// <summary>
    /// 安装为网络框架全局日志实现（进程级）。
    /// 应在创建任何 EntityManager 之前调用；Godot 进程在 ServiceLocator 静态初始化中安装，
    /// 独立 .NET 服务端进程在 Program.cs 中安装。
    /// </summary>
    public static void Install(ILogger logger) =>
        LiteEntitySystem.Logger.LoggerImpl = new LesNetworkLogger(logger);

    /// <summary>LES 框架普通日志，转发为 Information 等级。</summary>
    public void Log(string log) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("{Message}", log);
    }

    /// <summary>LES 框架警告日志，转发为 Warning 等级。</summary>
    public void LogWarning(string log) {
        if (_logger.IsEnabled(LogLevel.Warning))
            _logger.LogWarning("{Message}", log);
    }

    /// <summary>LES 框架错误日志，转发为 Error 等级。</summary>
    public void LogError(string log) {
        if (_logger.IsEnabled(LogLevel.Error))
            _logger.LogError("{Message}", log);
    }
}
