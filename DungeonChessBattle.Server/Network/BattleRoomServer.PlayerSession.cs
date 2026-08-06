using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Entities;
using LiteEntitySystem.Transport;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// BattleRoomServer 的玩家会话与断线重连管理。
/// 断线模型：断开仅标记 Disconnected 状态并保留实体，玩家可随时重连；
/// 房间销毁时由 <see cref="CleanupAllSessions"/> 统一销毁全部保留实体。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>
    /// 大厅层预注册玩家到房间（准备阶段调用）。白名单校验由 Store 实时查询承担，
    /// 此处仅预留会话聚合。客户端真正连接房间端口前调用。
    /// </summary>
    public void RegisterPlayer(string playerId, string playerName) {
        _sessions.GetOrAdd(playerId, _ => new PlayerSession(playerId, playerName));
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomServer:{RoomId}] Player '{PlayerName}' ({PlayerId}) pre-registered.", RoomId, playerName, playerId);
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
            _peerToPlayerId[peer.Id] = effectivePlayerId;

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] PlayerRoomEntity created: '{PlayerName}', peer={PeerId}",
                    RoomId, playerName, peer.Id);
        }
    }

    /// <summary>
    /// 处理玩家重连：将新网络连接绑定到已有的 PlayerSession（保留原实体与战斗状态）。
    /// </summary>
    private void HandlePlayerReconnect(NetPeer peer, string playerId) {
        if (!_sessions.TryGetValue(playerId, out var session) || session.Entity == null) {
            _logger.LogWarning("[RoomServer:{RoomId}] Reconnect: entity not found for playerId '{PlayerId}', treating as new.", RoomId, playerId);
            HandleNewPlayerConnect(peer, playerId);
            return;
        }

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
    /// 不触发 OnPeerDisconnected 的断线标记，不修改 PlayerState。
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
    /// 销毁全部保留实体（房间销毁时调用；由大厅线程经 Stop() 触发）。
    /// </summary>
    private void CleanupAllSessions() {
        foreach (var session in _sessions.Values) {
            session.Entity?.Destroy();
        }
        _sessions.Clear();
        _peerToPlayerId.Clear();
    }
}
