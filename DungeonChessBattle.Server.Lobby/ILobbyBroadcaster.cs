namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 大厅广播端口（依赖反转）：业务层通过本接口向房间内连接推送消息，
/// 不依赖任何具体传输（SignalR/其他）。实现由传输层（ASP.NET）提供。
/// </summary>
public interface ILobbyBroadcaster {
    /// <summary>将指定连接加入房间的广播分组。</summary>
    Task AddToRoomAsync(string connectionId, string roomId);

    /// <summary>向房间内所有连接广播一条消息。</summary>
    Task SendToRoomAsync(string roomId, string hubMethod, object? dto);
}
