using System.Collections.Concurrent;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Stores;

/// <summary>
/// 基于进程内 ConcurrentDictionary 的状态存储实现。
/// 收敛大厅级房间状态、玩家状态与准备单位数据的存储逻辑，
/// 供 GameServer 与 GameLobby 统一访问；线程安全。
/// </summary>
public sealed class InMemoryGameStateStore(ILoggerFactory loggerFactory) : IGameStateStore {
    private readonly ILogger<InMemoryGameStateStore> _logger = loggerFactory.CreateLogger<InMemoryGameStateStore>();

    /// <summary>房间配置注册表（招募板使用）。</summary>
    private readonly ConcurrentDictionary<string, GameRoom> _roomConfigs = new();

    /// <summary>房间密码字典。null 表示无密码房间。</summary>
    private readonly ConcurrentDictionary<string, string?> _roomPasswords = new();

    /// <summary>房主玩家名表：房间ID → 房主 displayName。</summary>
    private readonly ConcurrentDictionary<string, string> _roomHosts = new();

    /// <summary>玩家准备状态表：房间ID → (玩家名 → 是否已准备)。</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _roomReadyStates = new();

    /// <summary>房间内玩家的 peer 归属表：peerId → (房间ID, 玩家名)。</summary>
    private readonly ConcurrentDictionary<int, (string RoomId, string PlayerName)> _peerPlayers = new();

    /// <summary>房间内玩家的 playerId 映射表：房间ID → (玩家名 → playerId)。</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _roomPlayerIds = new();

    /// <summary>准备阶段单位数据表：房间ID → (单位名, 阵营, 玩家名) 列表。</summary>
    private readonly ConcurrentDictionary<string, List<(string UnitName, string Camp, string PlayerName)>> _prepareUnits = new();

    // ─── IRoomStateStore ───

    /// <inheritdoc />
    public bool TryRegisterRoom(string roomId, string? password, GameRoom config) {
        if (!_roomConfigs.TryAdd(roomId, config))
            return false;

        _roomPasswords[roomId] = password;
        _prepareUnits[roomId] = [];
        _roomReadyStates[roomId] = [];
        _roomPlayerIds[roomId] = [];

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Store] Room '{RoomId}' registered (prepare), HasPassword={HasPwd}, Title={Title}",
                roomId, password != null, config.Title);
        return true;
    }

    /// <inheritdoc />
    public bool RoomExists(string roomId) => _roomConfigs.ContainsKey(roomId);

    /// <inheritdoc />
    public GameRoom? GetRoomConfig(string roomId) {
        _roomConfigs.TryGetValue(roomId, out var config);
        return config;
    }

    /// <inheritdoc />
    public IReadOnlyList<RoomListing> ListActiveRooms() {
        return [.. _roomConfigs
            .Where(kvp => kvp.Value.Status != RoomStatus.Finished)
            .Select(kvp => RoomListing.FromGameRoom(kvp.Value))
            .OrderByDescending(r => r.CreatedAt)];
    }

    /// <inheritdoc />
    public void UpdateRoomStatus(string roomId, RoomStatus status) {
        if (_roomConfigs.TryGetValue(roomId, out var config))
            config.Status = status;
    }

    /// <inheritdoc />
    public void UpdatePlayerCount(string roomId, int count) {
        if (_roomConfigs.TryGetValue(roomId, out var config))
            config.CurrentPlayers = count;
    }

    /// <inheritdoc />
    public bool ValidateRoomPassword(string roomId, string? password) {
        if (!_roomPasswords.TryGetValue(roomId, out var storedPassword))
            return false;
        return storedPassword == null || storedPassword == password;
    }

    /// <inheritdoc />
    public void RemoveRoomState(string roomId) {
        _roomPasswords.TryRemove(roomId, out _);
        _roomConfigs.TryRemove(roomId, out _);
        _prepareUnits.TryRemove(roomId, out _);
        _roomHosts.TryRemove(roomId, out _);
        _roomReadyStates.TryRemove(roomId, out _);
        _roomPlayerIds.TryRemove(roomId, out _);
        // 清理 peerPlayers 中属于该房间的条目
        foreach (var kv in _peerPlayers) {
            if (kv.Value.RoomId == roomId)
                _peerPlayers.TryRemove(kv.Key, out _);
        }
    }

    /// <inheritdoc />
    public void ClearAllState() {
        _roomPasswords.Clear();
        _roomConfigs.Clear();
        _prepareUnits.Clear();
        _roomHosts.Clear();
        _roomReadyStates.Clear();
        _roomPlayerIds.Clear();
        _peerPlayers.Clear();
    }

    // ─── IPlayerStateStore ───

    /// <inheritdoc />
    public void SetRoomHost(string roomId, string hostName) {
        _roomHosts[roomId] = hostName;
        // 将房主登记为房间成员（准备状态默认未准备，房主的准备状态不参与全员判定）
        var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
        states.TryAdd(hostName, false);
    }

    /// <inheritdoc />
    public void RegisterRoomPlayer(string roomId, string playerName, string playerId, int peerId) {
        var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
        states.TryAdd(playerName, false);
        _peerPlayers[peerId] = (roomId, playerName);
        RegisterRoomPlayerId(roomId, playerName, playerId);
    }

    /// <inheritdoc />
    public void RegisterRoomPlayerId(string roomId, string playerName, string playerId) {
        if (string.IsNullOrEmpty(playerId))
            return;

        var ids = _roomPlayerIds.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, string>());
        ids[playerName] = playerId;
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetRoomPlayerIds(string roomId) {
        if (_roomPlayerIds.TryGetValue(roomId, out var ids))
            return new Dictionary<string, string>(ids);
        return [];
    }

    /// <inheritdoc />
    public void SetPlayerReady(string roomId, string playerName, bool ready) {
        if (_roomHosts.TryGetValue(roomId, out var hostName) && hostName == playerName)
            return;

        if (_roomReadyStates.TryGetValue(roomId, out var states))
            states[playerName] = ready;
    }

    /// <inheritdoc />
    public bool IsAllOthersReady(string roomId) {
        if (!_roomReadyStates.TryGetValue(roomId, out var states))
            return false;

        if (_roomHosts.TryGetValue(roomId, out var hostName)) {
            foreach (var kv in states) {
                if (kv.Key == hostName)
                    continue;
                if (!kv.Value)
                    return false;
            }
            return true;
        }

        // 无房主记录时退化为全部成员检查
        return states.Values.All(ready => ready);
    }

    /// <inheritdoc />
    public RoomStateSnapshot GetRoomState(string roomId) {
        string hostName = _roomHosts.TryGetValue(roomId, out var host) ? host : "";
        string dungeonName = _roomConfigs.TryGetValue(roomId, out var config) ? config.DungeonName : "";
        var players = new List<PlayerReadyState>();
        if (_roomReadyStates.TryGetValue(roomId, out var states)) {
            foreach (var kv in states)
                players.Add(new PlayerReadyState(kv.Key, kv.Value));
        }
        return new RoomStateSnapshot(hostName, dungeonName, players);
    }

    /// <inheritdoc />
    public bool IsPeerRoomHost(int peerId, string roomId) {
        if (!_peerPlayers.TryGetValue(peerId, out var entry))
            return false;
        if (entry.RoomId != roomId)
            return false;
        return _roomHosts.TryGetValue(roomId, out var host) && entry.PlayerName == host;
    }

    /// <inheritdoc />
    public string? GetPlayerNameForPeer(int peerId) {
        if (_peerPlayers.TryGetValue(peerId, out var entry))
            return entry.PlayerName;
        return null;
    }

    /// <inheritdoc />
    public string? RemovePlayerByPeer(int peerId) {
        if (!_peerPlayers.TryRemove(peerId, out var entry))
            return null;

        if (_roomReadyStates.TryGetValue(entry.RoomId, out var states)) {
            states.TryRemove(entry.PlayerName, out _);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Store] Player '{Player}' removed from room '{RoomId}' (disconnected)",
                    entry.PlayerName, entry.RoomId);
        }
        return entry.RoomId;
    }

    /// <inheritdoc />
    public bool AddPrepareUnit(string roomId, string unitName, string camp, string playerName) {
        if (!_prepareUnits.TryGetValue(roomId, out var units))
            return false;

        units.Add((unitName, camp, playerName));
        return true;
    }

    /// <inheritdoc />
    public bool RemovePrepareUnit(string roomId, string unitName, string camp) {
        if (!_prepareUnits.TryGetValue(roomId, out var units))
            return false;
        return units.RemoveAll(u => u.UnitName == unitName && u.Camp == camp) > 0;
    }

    /// <inheritdoc />
    public IReadOnlyList<UnitSelection> GetPrepareUnits(string roomId) {
        if (!_prepareUnits.TryGetValue(roomId, out var units))
            return [];
        return [.. units.Select(u => new UnitSelection(u.UnitName, u.Camp, u.PlayerName))];
    }
}
