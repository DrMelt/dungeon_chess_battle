using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Entities;
using LiteEntitySystem.Transport;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// BattleRoomServer 的玩家会话与断线重连管理。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>
    /// 大厅层预注册玩家到白名单。客户端真正连接房间端口前调用。
    /// </summary>
    public void RegisterPlayer(string playerId, string playerName) {
        _validPlayerIds[playerId] = 1;
        _sessions.GetOrAdd(playerId, _ => new PlayerSession(playerId, playerName));
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomServer:{RoomId}] Player '{PlayerName}' ({PlayerId}) pre-registered.", RoomId, playerName, playerId);
    }

    /// <summary>
    /// 大厅层查询玩家是否可重连（需在宽限期内）。
    /// </summary>
    public bool CanReconnect(string playerId) {
        return _sessions.TryGetValue(playerId, out var session) && session.DisconnectTime != null;
    }

    /// <summary>
    /// 更新已注册玩家的显示名（重连时可能更改）。
    /// </summary>
    public void UpdatePlayerName(string playerId, string playerName) {
        if (_sessions.TryGetValue(playerId, out var session)) {
            session.PlayerName = playerName;
            session.Entity?.PlayerName.Value = playerName;
        }
        else {
            // 预注册阶段（尚未创建 Entity + Session），创建 session
            _sessions[playerId] = new PlayerSession(playerId, playerName);
        }
    }

    /// <summary>
    /// 处理新玩家首次连接：创建 PlayerSession + PlayerRoomEntity。
    /// </summary>
    private void HandleNewPlayerConnect(NetPeer peer, string? connectionKey) {
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var netPlayer = EntityManager.AddPlayer(lesPeer);

        // 确定 playerId（优先使用连接密钥中的 playerId）
        string effectivePlayerId = (connectionKey != null && connectionKey != _connectionKey)
            ? connectionKey
            : $"auto_{peer.Id}";

        // 获取或新建 PlayerSession
        var session = _sessions.GetOrAdd(effectivePlayerId,
            _ => new PlayerSession(effectivePlayerId, $"Player_{effectivePlayerId[..Math.Min(effectivePlayerId.Length, 8)]}"));

        // 确定玩家显示名
        string playerName = session.PlayerName;

        // 创建 PlayerRoomEntity
        var playerEntity = EntityManager.AddEntity<PlayerRoomEntity>(e => {
            e.PlayerName.Value = playerName;
            e.PlayerState.Value = (byte)PlayerConnectionState.Connected;
            e.IsReady.Value = false;
            e.Camp.Value = string.Empty;
        });

        if (playerEntity != null) {
            session.PeerId = peer.Id;
            session.Entity = playerEntity;
            session.NetPlayer = netPlayer;
            session.DisconnectTime = null;
            _peerToPlayerId[peer.Id] = effectivePlayerId;

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] PlayerRoomEntity created: '{PlayerName}', peer={PeerId}",
                    RoomId, playerName, peer.Id);
        }
    }

    /// <summary>
    /// 处理玩家重连：将新网络连接绑定到已有的 PlayerSession。
    /// </summary>
    private void HandlePlayerReconnect(NetPeer peer, string playerId) {
        if (!_sessions.TryGetValue(playerId, out var session) || session.Entity == null) {
            _logger.LogWarning("[RoomServer:{RoomId}] Reconnect: entity not found for playerId '{PlayerId}', treating as new.", RoomId, playerId);
            HandleNewPlayerConnect(peer, playerId);
            return;
        }

        // 清除宽限期计时器
        session.DisconnectTime = null;

        // 恢复连接状态
        session.Entity.PlayerState.Value = (byte)PlayerConnectionState.Connected;

        // 重建网络层绑定
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var netPlayer = EntityManager.AddPlayer(lesPeer);
        session.PeerId = peer.Id;
        session.NetPlayer = netPlayer;
        _peerToPlayerId[peer.Id] = playerId;

        // 客户端通过 PlayerState SyncVar 从 Disconnected→Connected 的变化检测重连成功

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Player '{PlayerName}' ({PlayerId}) reconnected (peer={PeerId}).",
                RoomId, session.Entity.PlayerName.Value, playerId, peer.Id);
    }

    /// <summary>
    /// 清理旧连接的所有映射（用于新连接替换旧连接场景）。
    /// 不触发 OnPeerDisconnected 的宽限期逻辑，不修改 PlayerState。
    /// </summary>
    private void ReplaceExistingConnection(string playerId) {
        if (!_sessions.TryGetValue(playerId, out var session))
            return;

        int oldPeerId = session.PeerId;

        // 从 LES 框架移除旧玩家
        if (session.NetPlayer != null)
            EntityManager.RemovePlayer(session.NetPlayer);

        _peerToPlayerId.TryRemove(oldPeerId, out _);
        session.NetPlayer = null;
        session.Controller = null;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomServer:{RoomId}] Old peer {OldPeerId} disconnected for playerId '{PlayerId}' (replaced by new).",
                RoomId, oldPeerId, playerId);
    }

    /// <summary>
    /// 检查并清理超出宽限期的断连玩家。
    /// </summary>
    private void CleanupExpiredPlayers() {
        var now = DateTime.UtcNow;
        var expiredIds = _sessions
            .Where(kv => kv.Value.DisconnectTime.HasValue
                && (now - kv.Value.DisconnectTime.Value).TotalSeconds > ReconnectGracePeriodSeconds)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var playerId in expiredIds) {
            CleanupPlayer(playerId);
        }
    }

    /// <summary>
    /// 彻底清理指定玩家：从白名单移除，销毁 PlayerRoomEntity 和关联的 UnitPawn。
    /// </summary>
    private void CleanupPlayer(string playerId) {
        _validPlayerIds.TryRemove(playerId, out _);

        if (_sessions.TryRemove(playerId, out var session)) {
            // 销毁 LES Entity（InternalEntity.Destroy），同步移除到所有客户端
            session.Entity?.Destroy();
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] Player '{PlayerName}' ({PlayerId}) cleanup completed (timeout + entity destroyed).",
                    RoomId, session.PlayerName, playerId);
        }

        // 通知外部（GameLobby 可据此判断是否需要销毁空房间）
        PlayerRemoved?.Invoke(RoomId, playerId);
    }
}
