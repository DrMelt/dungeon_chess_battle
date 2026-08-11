using System.Net;
using System.Net.Sockets;
using DungeonChessBattle.Protocol.Enums;
using LiteNetLib;
using LiteEntitySystem.Transport;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// BattleRoomServer 的网络事件处理：连接验证、断线保留实体与 LES 数据反序列化。
/// 断线模型：玩家断开仅标记 Disconnected 状态并保留实体，玩家可随时重连；
/// 全部活跃连接断开后触发 RoomEmpty 由大厅线程销毁房间。
/// </summary>
public partial class BattleRoomServer {
    void INetEventListener.OnConnectionRequest(ConnectionRequest request) {
        string incomingKey = request.Data.GetString();

        // 验证：房间存在的登记成员或使用服务器连接密钥，向后兼容或调试模式
        // 连接资格实时查询 Store：房间存在期间登记成员可连接，房间销毁后拒绝
        if (incomingKey == _connectionKey || _stateStore.IsRoomMember(RoomId, incomingKey)) {
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
        // 提取连接时使用的密钥，即 playerId 或默认密钥
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
            // 执行重连流程，Disconnected 恢复或替换后重新绑定
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

        // 查找该 peer 对应的 playerId，通过反向索引
        _peerToPlayerId.TryRemove(peer.Id, out string? playerId);
        if (playerId != null && _sessions.TryGetValue(playerId, out var session)) {
            // 标记为断连状态，保留实体不销毁；玩家可凭 Store 成员身份随时重连
            session.Entity?.PlayerState.Value = (byte)PlayerConnectionState.Disconnected;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] Player '{PlayerId}' disconnected (entity retained).",
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

        // 无任何活跃连接且已完成首帧初始化 → 通知大厅销毁房间
        // 断线玩家实体保留：房间仍在、玩家可凭 Store 成员身份重连；
        // 全部活跃连接断开后房间失去存在意义，由大厅线程移除
        if (HasActiveConnections == false && _initialized.IsSet)
            RoomEmpty?.Invoke(RoomId);
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
        // 房间端口不处理 JSON 自定义包，所有逻辑走 LES RPC
    }

    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }
}
