using DungeonChessBattle.Protocol.Dtos;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// GameServer 的大厅业务转发门面：将大厅 SignalR 请求转发给 Server.Lobby 的
/// <see cref="DungeonChessBattle.Server.Lobby.GameLobby"/> 处理。
/// 战斗编排，开始战斗与断线重连，见 GameServer.MessageHandlers。
/// </summary>
public partial class GameServer {
    /// <summary>创建房间，大厅业务。</summary>
    public Task<LobbyResult> HandleCreateRoomAsync(string connectionId, CreateRoomRequest req)
        => _lobby.HandleCreateRoomAsync(connectionId, req);

    /// <summary>加入房间，大厅业务。</summary>
    public Task<LobbyResult> HandleJoinRoomAsync(string connectionId, JoinRoomRequest req)
        => _lobby.HandleJoinRoomAsync(connectionId, req);

    /// <summary>获取招募板房间列表，大厅业务。</summary>
    public Task<RoomListResult> HandleListRoomsAsync()
        => _lobby.HandleListRoomsAsync();

    /// <summary>准备阶段添加单位，大厅业务。</summary>
    public Task<LobbyResult> HandleAddPrepareUnitAsync(string connectionId, PrepareAddUnitRequest req)
        => _lobby.HandleAddPrepareUnitAsync(connectionId, req);

    /// <summary>准备阶段移除单位，大厅业务。</summary>
    public Task<LobbyResult> HandleRemovePrepareUnitAsync(string connectionId, PrepareRemoveUnitRequest req)
        => _lobby.HandleRemovePrepareUnitAsync(connectionId, req);

    /// <summary>设置准备状态，大厅业务。</summary>
    public Task<LobbyResult> HandleSetReadyAsync(string connectionId, PrepareReadyStateRequest req)
        => _lobby.HandleSetReadyAsync(connectionId, req);
}
