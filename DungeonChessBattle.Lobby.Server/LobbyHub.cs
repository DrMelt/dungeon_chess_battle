using DungeonChessBattle.Lobby.Protocol;
using DungeonChessBattle.Lobby.Protocol.Dtos;
using DungeonChessBattle.Server.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace DungeonChessBattle.Lobby.Server;

/// <summary>
/// 大厅 SignalR Hub，ASP.NET Core 网络端点。
/// 每个 Hub 方法对应一个请求，一律委托给 <see cref="ILobbyApplication"/>：
/// Hub 只做端点与身份透传，不含业务；服务端 → 客户端的广播由业务层经 <see cref="ILobbyBroadcaster"/> 端口推送。
/// 方法名经 <see cref="HubMethodNameAttribute"/> 绑定协议常量，与客户端调用名编译期对齐。
/// 回放不在本 Hub：它自带 HTTP 端点，身份由登录时签发的会话凭证承载，与大厅连接无关。
/// </summary>
/// <param name="server">大厅业务协调器，面向抽象契约。</param>
public class LobbyHub(ILobbyApplication server) : Hub {
    private readonly ILobbyApplication _server = server;

    /// <summary>创建房间请求。</summary>
    [HubMethodName(HubMethods.CreateRoom)]
    public Task<LobbyResult> CreateRoom(CreateRoomRequest req)
        => _server.HandleCreateRoomAsync(Context.ConnectionId, req);

    /// <summary>加入房间请求。</summary>
    [HubMethodName(HubMethods.JoinRoom)]
    public Task<LobbyResult> JoinRoom(JoinRoomRequest req)
        => _server.HandleJoinRoomAsync(Context.ConnectionId, req);

    /// <summary>获取招募板房间列表请求。</summary>
    [HubMethodName(HubMethods.ListRooms)]
    public Task<RoomListResult> ListRooms()
        => _server.HandleListRoomsAsync();

    /// <summary>准备阶段：添加单位请求。</summary>
    [HubMethodName(HubMethods.AddPrepareUnit)]
    public Task<LobbyResult> AddPrepareUnit(PrepareAddUnitRequest req)
        => _server.HandleAddPrepareUnitAsync(Context.ConnectionId, req);

    /// <summary>准备阶段：移除单位请求。</summary>
    [HubMethodName(HubMethods.RemovePrepareUnit)]
    public Task<LobbyResult> RemovePrepareUnit(PrepareRemoveUnitRequest req)
        => _server.HandleRemovePrepareUnitAsync(Context.ConnectionId, req);

    /// <summary>准备阶段：开始战斗请求，房间与发起者由连接归属反查。</summary>
    [HubMethodName(HubMethods.StartBattle)]
    public Task<LobbyResult> StartBattle()
        => _server.HandleStartBattleAsync(Context.ConnectionId);

    /// <summary>准备阶段：设置是否已准备请求，房间与玩家由连接归属反查。</summary>
    [HubMethodName(HubMethods.SetReady)]
    public Task<LobbyResult> SetReady(PrepareReadyStateRequest req)
        => _server.HandleSetReadyAsync(Context.ConnectionId, req);

    /// <summary>重连房间请求。</summary>
    [HubMethodName(HubMethods.ReconnectRoom)]
    public Task<LobbyResult> ReconnectRoom(ReconnectRoomRequest req)
        => _server.HandleReconnectRoomAsync(Context.ConnectionId, req);

    /// <summary>离开房间请求，房间由连接归属反查。</summary>
    [HubMethodName(HubMethods.LeaveRoom)]
    public Task<LobbyResult> LeaveRoom()
        => _server.HandleLeaveRoomAsync(Context.ConnectionId);

    /// <summary>登入大厅请求，登记服务端权威玩家名。</summary>
    [HubMethodName(HubMethods.Login)]
    public Task<LoginResult> Login(LoginRequest req)
        => _server.HandleLoginAsync(Context.ConnectionId, req);

    /// <summary>连接断开时清理玩家归属。</summary>
    public override async Task OnDisconnectedAsync(Exception? exception) {
        await _server.ConnectionLostAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
