using DungeonChessBattle.Protocol;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 服务器装配配置的唯一来源。
/// 收敛端口、连接密钥、服务器密码等原本散落在各网络类中的常量，
/// 由入口 Program 与 GameServerHost 构建一次后映射为各模块的配置切片注入
/// <see cref="Lobby.LobbyServerConfig"/> 与 <see cref="Battle.BattleServerConfig"/>。
/// </summary>
public sealed record ServerConfig {
    /// <summary>默认大厅监听端口，唯一来源，替代散落各处的 10170 常量。</summary>
    public const int DefaultPort = NetworkDefaults.LobbyPort;

    /// <summary>大厅监听端口。</summary>
    public int LobbyPort { get; init; } = DefaultPort;

    /// <summary>默认连接密钥，客户端连接服务器时的握手密钥；有服务器密码时优先用密码。</summary>
    public string ConnectionKey { get; init; } = NetworkDefaults.ConnectionKey;

    /// <summary>服务器访问密码；为空表示不启用。</summary>
    public string? ServerPassword {
        get; init;
    }

    /// <summary>房间端口池起点，大厅端口之后。</summary>
    public int FirstRoomPort { get; init; } = 10171;

    /// <summary>
    /// 从环境变量构建服务器装配配置。
    /// 服务器密码从 <see cref="ServerProcessEnv.Password"/> 读取；显式传入的密码优先。
    /// </summary>
    /// <param name="serverPassword">显式服务器密码；为空时回退到环境变量。</param>
    public static ServerConfig FromEnvironment(string? serverPassword = null) {
        string? actualPassword = string.IsNullOrEmpty(serverPassword)
            ? Environment.GetEnvironmentVariable(ServerProcessEnv.Password)
            : serverPassword;

        return new ServerConfig {
            ServerPassword = string.IsNullOrEmpty(actualPassword) ? null : actualPassword,
        };
    }
}
