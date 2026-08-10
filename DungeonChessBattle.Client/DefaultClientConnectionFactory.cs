using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Client.Lobby;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 默认连接工厂：创建实际的 SignalR 大厅客户端与 LES 房间客户端。
/// </summary>
public sealed class DefaultClientConnectionFactory : IClientConnectionFactory {
    /// <inheritdoc />
    public LobbyClient CreateLobbyClient(ILogger<LobbyClient> logger) => new(logger);

    /// <inheritdoc />
    public RoomBattleClient CreateRoomBattleClient(ILogger<RoomBattleClient> logger) => new(logger);
}
