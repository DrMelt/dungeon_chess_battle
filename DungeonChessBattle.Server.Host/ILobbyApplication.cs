using DungeonChessBattle.Protocol.Dtos;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 大厅应用服务契约（协调层抽象）：SignalR Hub 端点面向本接口，
/// 隐藏具体协调实现，便于测试与替换。
/// </summary>
public interface ILobbyApplication {
    /// <summary>创建房间。</summary>
    Task<LobbyResult> HandleCreateRoomAsync(string connectionId, CreateRoomRequest req);

    /// <summary>加入房间。</summary>
    Task<LobbyResult> HandleJoinRoomAsync(string connectionId, JoinRoomRequest req);

    /// <summary>获取招募板房间列表。</summary>
    Task<RoomListResult> HandleListRoomsAsync();

    /// <summary>准备阶段添加单位。</summary>
    Task<LobbyResult> HandleAddPrepareUnitAsync(string connectionId, PrepareAddUnitRequest req);

    /// <summary>准备阶段移除单位。</summary>
    Task<LobbyResult> HandleRemovePrepareUnitAsync(string connectionId, PrepareRemoveUnitRequest req);

    /// <summary>开始战斗。</summary>
    Task<LobbyResult> HandleStartBattleAsync(string connectionId, PrepareStartBattleRequest req);

    /// <summary>设置准备状态。</summary>
    Task<LobbyResult> HandleSetReadyAsync(string connectionId, PrepareReadyStateRequest req);

    /// <summary>重连房间。</summary>
    Task<LobbyResult> HandleReconnectRoomAsync(ReconnectRoomRequest req);

    /// <summary>离开房间（准备阶段主动退出）。</summary>
    Task<LobbyResult> HandleLeaveRoomAsync(string connectionId, LeaveRoomRequest req);

    /// <summary>连接断开清理。</summary>
    Task ConnectionLostAsync(string connectionId);
}
