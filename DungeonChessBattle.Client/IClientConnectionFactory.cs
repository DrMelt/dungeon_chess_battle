using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Client.Lobby;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 客户端连接工厂：创建大厅（SignalR）与房间（LES）客户端实例。
/// 门面经本接口创建底层连接客户端，不直接依赖具体传输实现，便于替换与测试。
/// </summary>
public interface IClientConnectionFactory {
    /// <summary>创建大厅客户端。</summary>
    LobbyClient CreateLobbyClient(ILogger<LobbyClient> logger);

    /// <summary>创建房间客户端。</summary>
    RoomBattleClient CreateRoomBattleClient(ILogger<RoomBattleClient> logger);
}
