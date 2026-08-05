using System.Net;
using System.Net.Sockets;
using DungeonChessBattle.Core.Enums;
using LiteNetLib;
using LiteEntitySystem.Transport;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// BattleRoomServer 的网络事件处理：连接验证、断线宽限期与 LES 数据反序列化。
/// </summary>
public partial class BattleRoomServer {
    void INetEventListener.OnConnectionRequest(ConnectionRequest request) {
        string incomingKey = request.Data.GetString();

        // 验证：playerId 在白名单中 或 使用服务器连接密钥（向后兼容/调试模式）
        if (incomingKey == _connectionKey || _validPlayerIds.ContainsKey(incomingKey)) {
            _acceptedKeys.Enqueue(incomingKey);
            request.Accept();
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RoomServer:{RoomId}] Connection rejected: invalid key '{Key}' from {RemoteEP}", RoomId, incomingKey, request.RemoteEndPoint);
            request.Reject();
        }
    }

    void INetEventListener.OnPeerConnected(NetPeer peer) {
        // 提取连接时使用的密钥（即 playerId 或默认密钥）
        _acceptedKeys.TryDequeue(out string? connectionKey);

        // P1 修复：同一 playerId 已有活跃连接时，关闭旧连接接受新连接
        if (connectionKey != null && connectionKey != _connectionKey
            && _sessions.TryGetValue(connectionKey, out var existingSession)
            && existingSession.Entity != null) {
            if (existingSession.Entity.PlayerState.Value == (byte)PlayerConnectionState.Connected) {
                // 替换：清理旧 peer，用新 peer 重连
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("[RoomServer:{RoomId}] Duplicate connection for playerId '{PlayerId}', replacing old peer.",
                    RoomId, connectionKey);
                ReplaceExistingConnection(connectionKey);
            }
            // 执行重连流程（Disconnected → 恢复 或 替换后重新绑定）
            HandlePlayerReconnect(peer, connectionKey);
        }
        else {
            HandleNewPlayerConnect(peer, connectionKey);
        }

        OnClientConnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (peer.Tag is LiteNetLibNetPeer lesPeer)
            EntityManager.RemovePlayer(lesPeer);

        // 查找该 peer 对应的 playerId（通过反向索引）
        _peerToPlayerId.TryRemove(peer.Id, out string? playerId);
        if (playerId != null && _sessions.TryGetValue(playerId, out var session)) {
            // 标记为断连状态（保留 Entity，不销毁）
            session.Entity?.PlayerState.Value = (byte)PlayerConnectionState.Disconnected;
            session.DisconnectTime = DateTime.UtcNow;
            // 注意：不删除 _sessions 和 _validPlayerIds
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] Player '{PlayerId}' disconnected (reconnect grace period started).",
                    RoomId, playerId);
        }

        // 清除旧 peer 的引用
        if (playerId != null && _sessions.TryGetValue(playerId, out var s)) {
            s.NetPlayer = null;
            s.Controller = null;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Peer disconnected: {PeerId}, playerId={PlayerId}, Reason={Reason}",
                RoomId, peer.Id, playerId, disconnectInfo.Reason);

        OnClientDisconnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        _logger.LogError("[RoomServer:{RoomId}] Error: {SocketError} from {EndPoint}", RoomId, socketError, endPoint);
    }

    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var data = reader.GetRemainingBytes();
        if (data.Length > 0 && data[0] == PacketHeader) {
            if (peer.Tag is LiteNetLibNetPeer lesPeer)
                EntityManager.Deserialize(lesPeer, data);
        }
        // 房间端口不处理 JSON 自定义包（所有逻辑走 LES RPC）
    }

    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }
}
