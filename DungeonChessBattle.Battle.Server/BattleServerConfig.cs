using DungeonChessBattle.Battle.Entities;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// 战斗侧配置切片：仅包含战斗房间所需的字段。
/// 由装配层 Server.Host 从服务器装配配置 <c>ServerConfig</c> 映射后注入，
/// 战斗模块不感知大厅密码等大厅侧配置。
/// </summary>
public sealed record BattleServerConfig {
    /// <summary>默认连接密钥，客户端连接房间时的握手密钥；优先级计算在装配层完成。</summary>
    public string ConnectionKey { get; init; } = NetworkDefaults.ConnectionKey;

    /// <summary>房间端口池起点，大厅端口之后。</summary>
    public int FirstRoomPort { get; init; } = 10171;
}
