using DungeonChessBattle.Server.Domain.Lobby;
using Microsoft.AspNetCore.SignalR;

namespace DungeonChessBattle.Server;

/// <summary>
/// <see cref="ILobbyBroadcaster"/> 的 SignalR 实现（ASP.NET 传输层）。
/// 将领域广播端口映射到 SignalR Group，供业务层（Domain）使用。
/// </summary>
/// <param name="hub">SignalR Hub 上下文。</param>
public sealed class SignalRBroadcaster(IHubContext<LobbyHub> hub) : ILobbyBroadcaster {
    private readonly IHubContext<LobbyHub> _hub = hub;

    /// <inheritdoc />
    public Task AddToRoomAsync(string connectionId, string roomId)
        => _hub.Groups.AddToGroupAsync(connectionId, roomId);

    /// <inheritdoc />
    public Task SendToRoomAsync(string roomId, string hubMethod, object? dto)
        => _hub.Clients.Group(roomId).SendAsync(hubMethod, dto);
}
