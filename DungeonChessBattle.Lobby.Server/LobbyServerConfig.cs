namespace DungeonChessBattle.Lobby.Server;

/// <summary>
/// 大厅侧配置切片：仅包含大厅业务 GameLobby 所需的字段。
/// 由装配层 Server.Host 从服务器装配配置 <c>ServerConfig</c> 映射后注入，
/// 大厅模块不感知战斗侧配置。
/// </summary>
public sealed record LobbyServerConfig {
    /// <summary>服务器访问密码；为空表示不启用。</summary>
    public string? ServerPassword {
        get; init;
    }
}
