using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Shared.Inputs;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// BattleRoomServer 的玩家会话与断线重连管理。
/// 断线模型：断开仅清空会话连接状态并保留单位与战斗状态，玩家可随时重连；
/// 房间销毁时由 <see cref="CleanupAllSessions"/> 统一清理会话。
/// 连接状态是会话本地数据，不产生网络同步实体。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>
    /// 大厅层登记玩家到房间，断线重连专用：仅当房间已有该 playerId 的会话
    /// 且会话玩家名与登录名一致才允许，杜绝客户端自报 playerId 冒用他人单位。
    /// 首次进入战斗的玩家不经此路径，由连接流程创建会话。
    /// </summary>
    public bool RegisterPlayer(string playerId, string playerName) {
        if (_sessions.TryGetValue(playerId, out var session) && session.PlayerName == playerName)
            return true;

        if (_logger.IsEnabled(LogLevel.Warning))
            _logger.LogWarning("[RoomId: {RoomId}] Reconnect rejected: session for '{PlayerId}' missing or name mismatch (session='{Existing}', requested='{Requested}').",
                RoomId, playerId, session?.PlayerName, playerName);
        return false;
    }

    /// <summary>
    /// 处理新玩家首次连接：创建 PlayerSession 并绑定控制器。
    /// 连接状态由 PeerId 表达，连接即置位，断线清零，无网络同步实体。
    /// </summary>
    private void HandleNewPlayerConnect(NetPeer peer, string? connectionKey) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] New player connect: peer={PeerId}, connectionKey={Key}",
                RoomId, peer.Id, connectionKey ?? "(null)");

        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var netPlayer = EntityManager.AddPlayer(lesPeer);

        // 确定 playerId，优先使用连接密钥中的 playerId
        string effectivePlayerId = (connectionKey != null && connectionKey != _connectionKey)
            ? connectionKey
            : $"auto_{peer.Id}";

        // 获取或新建 PlayerSession
        var session = _sessions.GetOrAdd(effectivePlayerId,
            _ => new PlayerSession(effectivePlayerId, $"Player_{effectivePlayerId[..Math.Min(effectivePlayerId.Length, 8)]}"));

        session.PeerId = peer.Id;
        session.NetPlayer = netPlayer;
        _peerToPlayerId[peer.Id] = effectivePlayerId;

        // 将该玩家与其在准备阶段选择的单位绑定，创建 UnitController
        TryBindPlayerController(session, netPlayer);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Player session established: '{PlayerName}', peer={PeerId}",
                RoomId, session.PlayerName, peer.Id);
    }

    /// <summary>
    /// 处理玩家重连：将新网络连接绑定到已有的 PlayerSession，保留单位与战斗状态。
    /// </summary>
    private void HandlePlayerReconnect(NetPeer peer, string playerId) {
        if (!_sessions.TryGetValue(playerId, out var session)) {
            _logger.LogWarning("[RoomId: {RoomId}] Reconnect: session not found for playerId '{PlayerId}', treating as new.", RoomId, playerId);
            HandleNewPlayerConnect(peer, playerId);
            return;
        }

        // 重建网络层绑定
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var netPlayer = EntityManager.AddPlayer(lesPeer);
        session.PeerId = peer.Id;
        session.NetPlayer = netPlayer;
        _peerToPlayerId[peer.Id] = playerId;

        // 重连时重新建立控制器绑定，原 Controller 已随旧连接清理
        TryBindPlayerController(session, netPlayer);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Player '{PlayerName}' ({PlayerId}) reconnected (peer={PeerId}).",
                RoomId, session.PlayerName, playerId, peer.Id);
    }

    /// <summary>
    /// 将玩家与其在准备阶段选择的单位绑定：创建 UnitController 并交由 LES 管理。
    /// 首次连接与断线重连共用。仅房间线程调用。
    /// 玩家未选单位时跳过，如房主或观战，输入自然被忽略。
    /// </summary>
    private void TryBindPlayerController(PlayerSession session, NetPlayer netPlayer) {
        // 1. 按玩家持久 ID 匹配准备阶段选择的单位，连接密钥即 playerId，零竞态
        var selection = _stateStore.GetPrepareUnits(RoomId)
            .FirstOrDefault(s => s.PlayerId == session.PlayerId);
        if (selection == null) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[RoomId: {RoomId}] Player '{PlayerName}' has no prepare unit, controller skipped.",
                    RoomId, session.PlayerName);
            return;
        }

        // 权威玩家名来自准备单位登记，与大厅登录会话一致；首次连接时落定，重连校验依赖
        session.PlayerName = selection.PlayerName;

        // 2. 取该玩家专属 Pawn，重名单位不串绑
        if (!_pawnByPlayerId.TryGetValue(session.PlayerId, out var pawn)) {
            _logger.LogWarning("[RoomId: {RoomId}] Player '{PlayerName}' prepare unit '{UnitName}' pawn not found.",
                RoomId, session.PlayerName, selection.UnitConfigKey);
            return;
        }

        // 载体已销毁时绑定不报错，只会静默失去输入通道（PawnLogic.Update 跳过已销毁实体），响亮拒绝
        if (pawn.IsDestroyed) {
            _logger.LogError("[RoomId: {RoomId}] Bind skipped: pawn {Unit} (netId={NetId}) of player '{PlayerName}' is destroyed.",
                RoomId, pawn.UnitKeyName.Value, pawn.Id, session.PlayerName);
            return;
        }

        // 3. 创建控制器并绑定到该单位，LiteEntitySystem 标准 API：技能施放与聚焦请求到达时转成玩家命令，
        //    权威判定与回放录制共用同一提交结论，框架自动按该控制器所属玩家回发回执。
        EntityManager.AddController<UnitController>(netPlayer, pawn, c => {
            c.BindServerCastHandler(req => SubmitAndRecord(PlayerCommand.Cast(
                pawn.Id, req.SkillTypeId, req.TargetNetId, req.TargetPosX, req.TargetPosZ)));
            c.BindServerFocusHandler(req => SubmitAndRecord(
                PlayerCommand.Focus(pawn.Id, req.TargetUnitNetId)));
            session.Controller = c;
        });

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Bound controller: player '{PlayerName}' -> unit '{UnitName}' (campOption={CampOptionKey}).",
                RoomId, session.PlayerName, selection.UnitConfigKey, selection.CampOptionKey);
    }

    /// <summary>
    /// 清理旧连接的所有映射，用于新连接替换旧连接场景。
    /// 不触发 OnPeerDisconnected 的断线标记；会话连接状态由 HandlePlayerReconnect 重新置位。
    /// </summary>
    private void ReplaceExistingConnection(string playerId) {
        if (!_sessions.TryGetValue(playerId, out var session))
            return;

        int oldPeerId = session.PeerId;

        // 解绑控制先于移除玩家：否则 LES 连带销毁该玩家的单位载体
        ReleaseControlledPawn(session);

        // 从 LES 框架移除旧玩家
        if (session.NetPlayer != null)
            EntityManager.RemovePlayer(session.NetPlayer);

        _peerToPlayerId.TryRemove(oldPeerId, out _);
        session.PeerId = 0;
        session.NetPlayer = null;
        session.Controller = null;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomId: {RoomId}] Old peer {OldPeerId} disconnected for playerId '{PlayerId}' (replaced by new).",
                RoomId, oldPeerId, playerId);
    }

    /// <summary>
    /// 解除玩家对单位载体的控制：LES 的 <c>RemovePlayer</c> 经 <c>DestroyWithControlledEntity</c>
    /// 连带销毁受控实体，而本项目单位属房间不属玩家，故先行 <c>StopControl</c> 解绑，使其只销毁控制器。
    /// 解绑后该单位失去移动意图来源，下一 tick 即静止。
    /// 仅房间线程调用，且必须先于 <c>RemovePlayer</c>。
    /// </summary>
    private static void ReleaseControlledPawn(PlayerSession session) {
        session.Controller?.StopControl();
    }

    /// <summary>
    /// 清理全部会话与连接映射，房间销毁时调用，由大厅线程经 Stop() 触发。
    /// 单位与控制器实体随房间销毁统一清理。
    /// </summary>
    private void CleanupAllSessions() {
        _sessions.Clear();
        _peerToPlayerId.Clear();
    }
}
