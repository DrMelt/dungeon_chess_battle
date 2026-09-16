namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// 战斗侧配置切片：仅包含战斗房间所需的字段。
/// 由装配层 Server.Host 从服务器装配配置 <c>ServerConfig</c> 映射后注入，
/// 战斗模块不感知大厅密码等大厅侧配置。
/// 战斗机制参数（如施法预输入窗口）不在此列：那些值必须服务端与回放同值，属领域常量。
/// </summary>
public sealed record BattleServerConfig {
    /// <summary>房间端口默认握手指纹，服务端未配置密码时使用；客户端连接房间须与之相符。</summary>
    public const string DefaultConnectionKey = "DungeonChessBattle";

    /// <summary>连接密钥，客户端连接房间时的握手密钥；优先级计算在装配层完成。</summary>
    public string ConnectionKey { get; init; } = DefaultConnectionKey;

    /// <summary>房间端口池起点，取大厅默认端口之后一位。与 <c>LobbyTransport.DefaultPort</c> 分属不同程序集，无编译期关联，改大厅默认端口须同步此值。</summary>
    public int FirstRoomPort { get; init; } = 10171;
}
