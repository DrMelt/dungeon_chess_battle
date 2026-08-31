using DungeonChessBattle.Lobby.Protocol.Dtos;

namespace DungeonChessBattle.Client;

/// <summary>
/// GameClientService 的大厅房间快照只读视图，作为客户端房间领域的单一事实源。
/// 创建/加入/重连路径都会写 _cachedRoomId，LeaveRoom/Disconnect 会清空；
/// 房间权威快照由 LobbyClient 缓存并经 OnRoomSnapshotUpdated 更新。
/// 与门面的 RoomSession 属性无关：后者是房间 LES 战斗链路契约。
/// </summary>
public sealed partial class GameClientService {
    /// <summary>当前所在房间 ID；未进房间时为 null。</summary>
    public string? CurrentRoomId => _cachedRoomId;

    /// <summary>当前房间最近一次权威快照；未进房间或快照未到时为 null。</summary>
    public RoomSnapshot? CurrentRoomSnapshot =>
        string.IsNullOrEmpty(_cachedRoomId) ? null : GetRoomSnapshot(_cachedRoomId);

    /// <summary>当前玩家是否为房主（房间未定或快照未到时为 false）。</summary>
    public bool IsCurrentUserHost => CurrentRoomSnapshot is { } s && s.HostName == PlayerName;

    /// <summary>当前玩家是否已准备（快照未到或未在玩家列表时为 false）。</summary>
    public bool IsCurrentUserReady {
        get {
            var snapshot = CurrentRoomSnapshot;
            if (snapshot == null)
                return false;
            foreach (var p in snapshot.Players)
                if (p.PlayerName == PlayerName)
                    return p.Ready;
            return false;
        }
    }

    /// <summary>当前玩家是否已选择单位。</summary>
    public bool HasCurrentUserUnit => CurrentRoomSnapshot is { } s
        && s.Units.Any(u => u.PlayerName == PlayerName);

    /// <summary>除房主外其他玩家是否全部已准备；无其他玩家视为满足。</summary>
    public bool OthersReady {
        get {
            var snapshot = CurrentRoomSnapshot;
            if (snapshot == null)
                return true;
            foreach (var p in snapshot.Players) {
                if (p.PlayerName == snapshot.HostName)
                    continue;
                if (!p.Ready)
                    return false;
            }
            return true;
        }
    }
}
