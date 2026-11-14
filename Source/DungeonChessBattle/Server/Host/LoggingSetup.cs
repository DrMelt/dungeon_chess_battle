namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 日志配置集中点。进程级前置日志（mod 装配、LES）与 ASP.NET Core 宿主日志
/// 复用同一套控制台格式与最低级别，避免两处配置漂移。
/// </summary>
public static class LoggingSetup {
    /// <summary>进程级最低日志级别。</summary>
    public const LogLevel MinimumLevel = LogLevel.Debug;

    /// <summary>配置控制台日志：单行、时间戳与最低级别。</summary>
    public static void ConfigureConsole(this ILoggingBuilder builder, LogLevel minimumLevel = MinimumLevel) {
        builder.AddSimpleConsole(options => {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss.fff ";
        });
        builder.SetMinimumLevel(minimumLevel);
    }
}
