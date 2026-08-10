using DungeonChessBattle.Protocol.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 大厅 SignalR Hub（ASP.NET Core 网络端点）。
/// 每个 Hub 方法对应一个大厅请求，委托给 <see cref="GameServer"/> 业务协调器处理；
/// 服务端 → 客户端的广播由 GameServer 经 IHubContext 的 Group 推送。
/// </summary>
/// <param name="server">游戏服务端业务协调器（面向抽象契约）。</param>
public class LobbyHub(ILobbyApplication server) : Hub {
    private readonly ILobbyApplication _server = server;

    /// <summary>创建房间请求。</summary>
    public Task<LobbyResult> CreateRoom(CreateRoomRequest req)
        => _server.HandleCreateRoomAsync(Context.ConnectionId, req);

    /// <summary>加入房间请求。</summary>
    public Task<LobbyResult> JoinRoom(JoinRoomRequest req)
        => _server.HandleJoinRoomAsync(Context.ConnectionId, req);

    /// <summary>获取招募板房间列表请求。</summary>
    public Task<RoomListResult> ListRooms()
        => _server.HandleListRoomsAsync();

    /// <summary>准备阶段：添加单位请求。</summary>
    public Task<LobbyResult> AddPrepareUnit(PrepareAddUnitRequest req)
        => _server.HandleAddPrepareUnitAsync(Context.ConnectionId, req);

    /// <summary>准备阶段：移除单位请求。</summary>
    public Task<LobbyResult> RemovePrepareUnit(PrepareRemoveUnitRequest req)
        => _server.HandleRemovePrepareUnitAsync(Context.ConnectionId, req);

    /// <summary>准备阶段：开始战斗请求。</summary>
    public Task<LobbyResult> StartBattle(PrepareStartBattleRequest req)
        => _server.HandleStartBattleAsync(Context.ConnectionId, req);

    /// <summary>准备阶段：设置是否已准备请求。</summary>
    public Task<LobbyResult> SetReady(PrepareReadyStateRequest req)
        => _server.HandleSetReadyAsync(Context.ConnectionId, req);

    /// <summary>重连房间请求。</summary>
    public Task<LobbyResult> ReconnectRoom(ReconnectRoomRequest req)
        => _server.HandleReconnectRoomAsync(req);

    /// <summary>连接断开时清理玩家归属。</summary>
    public override async Task OnDisconnectedAsync(Exception? exception) {
        await _server.ConnectionLostAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
