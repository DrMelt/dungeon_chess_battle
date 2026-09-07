using Microsoft.AspNetCore.SignalR;

namespace DungeonChessBattle.Lobby.Server;

/// <summary>
/// SignalR 房间内广播实现，ASP.NET 传输层。
/// 将广播映射到 SignalR Group，供业务层使用。
/// </summary>
/// <param name="hub">SignalR Hub 上下文。</param>
public sealed class SignalRBroadcaster(IHubContext<LobbyHub> hub) {
    private readonly IHubContext<LobbyHub> _hub = hub;

    /// <summary>将连接加入房间广播分组。</summary>
    public Task AddToRoomAsync(string connectionId, string roomId)
        => _hub.Groups.AddToGroupAsync(connectionId, roomId);

    /// <summary>将连接移出房间广播分组。</summary>
    public Task RemoveFromRoomAsync(string connectionId, string roomId)
        => _hub.Groups.RemoveFromGroupAsync(connectionId, roomId);

    /// <summary>向房间广播分组发送协议报文。</summary>
    public Task SendToRoomAsync(string roomId, string hubMethod, object? dto)
        => _hub.Clients.Group(roomId).SendAsync(hubMethod, dto);
}
