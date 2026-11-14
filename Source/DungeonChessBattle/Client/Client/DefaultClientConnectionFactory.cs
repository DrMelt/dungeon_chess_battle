using DungeonChessBattle.Battle.Client;
using DungeonChessBattle.Battle.Config.Shared;
using DungeonChessBattle.Lobby.Client;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 默认连接工厂：创建实际的 SignalR 大厅客户端与 LES 房间客户端。
/// 房间客户端要按内容装配领域单位与副本布局，故工厂持内容只读视图。
/// </summary>
/// <param name="content">内容注册表只读视图，房间客户端装配领域单位与副本布局的来源。</param>
public sealed class DefaultClientConnectionFactory(IContentRegistryView content)
    : IClientConnectionFactory {
    /// <inheritdoc />
    public LobbyClient CreateLobbyClient(ILogger<LobbyClient> logger) => new(logger);

    /// <inheritdoc />
    public RoomBattleClient CreateRoomBattleClient(ILogger<RoomBattleClient> logger) =>
        new(logger, content);
}
