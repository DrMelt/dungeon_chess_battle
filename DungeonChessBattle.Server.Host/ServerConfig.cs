using DungeonChessBattle.Battle.Entities;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 服务器装配配置唯一来源。
/// 由入口从命令行参数与环境变量一次解析，映射为各模块配置切片注入。
/// 环境变量名与子进程跨进程契约 <see cref="ServerProcessEnv"/> 保持单一来源。
/// </summary>
public sealed record ServerConfig {
    /// <summary>默认大厅监听端口。</summary>
    public const int DefaultPort = NetworkDefaults.LobbyPort;

    /// <summary>大厅监听端口。</summary>
    public int LobbyPort { get; init; } = DefaultPort;

    /// <summary>服务器访问密码；为空表示不启用。</summary>
    public string? ServerPassword { get; init; }

    /// <summary>mods 根目录绝对路径；为空表示纯内置内容。</summary>
    public string? ModDir { get; init; }

    /// <summary>父进程 PID；为空表示独立运行模式。</summary>
    public int? ParentPid { get; init; }

    /// <summary>
    /// 从命令行参数与环境变量构建服务器装配配置。
    /// 命令行：--port &lt;端口&gt;、--mod-dir &lt;路径&gt;；环境变量见 <see cref="ServerProcessEnv"/>。
    /// </summary>
    public static ServerConfig Load(string[] args) {
        int port = DefaultPort;
        if (int.TryParse(GetArg(args, "--port"), out int parsedPort) && parsedPort is > 0 and <= 65535)
            port = parsedPort;

        string? modDir = GetArg(args, "--mod-dir")
            ?? Environment.GetEnvironmentVariable(ServerProcessEnv.ModDir);

        string? password = Environment.GetEnvironmentVariable(ServerProcessEnv.Password);

        int? parentPid = null;
        if (int.TryParse(Environment.GetEnvironmentVariable(ServerProcessEnv.ParentPid), out int parsedPid)
            && parsedPid > 0)
            parentPid = parsedPid;

        return new ServerConfig {
            LobbyPort = port,
            ServerPassword = string.IsNullOrEmpty(password) ? null : password,
            ModDir = string.IsNullOrEmpty(modDir) ? null : modDir,
            ParentPid = parentPid,
        };
    }

    /// <summary>读取命令行参数 name 的下一个值。</summary>
    private static string? GetArg(string[] args, string name) {
        for (int i = 0; i < args.Length - 1; i++) {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}
