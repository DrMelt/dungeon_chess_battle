using System.Collections.Concurrent;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Stores;

/// <summary>
/// 基于进程内 ConcurrentDictionary 的状态存储实现。
/// 收敛大厅级房间状态、玩家状态与准备单位数据的存储逻辑，
/// 供 GameServer 与 GameLobby 统一访问；线程安全。
/// 并发策略：外层容器使用 ConcurrentDictionary 保证条目原子读写；
/// 同一房间内的读改写（含 List&lt;T&gt; 与可变模型字段）统一经房间级锁
/// 串行化（见 <see cref="GetRoomLock"/>），避免 ConcurrentDictionary
/// 不保证 value 对象线程安全的问题。跨房间的枚举（招募板）采用弱一致性快照。
/// </summary>
public sealed class InMemoryGameStateStore(ILoggerFactory loggerFactory) : IGameStateStore, IDisposable {
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

    /// <summary>准备阶段单位数据表：房间ID → (单位名, 阵营, 玩家名, 玩家ID) 列表。</summary>
    private readonly ConcurrentDictionary<string, List<(string UnitName, string Camp, string PlayerName, string PlayerId)>> _prepareUnits = new();

    /// <summary>
    /// 房间级锁表：房间ID → 锁对象，串行化同一房间的读改写，保证
    /// List&lt;T&gt; 操作、可变模型字段读改写、peer 归属清理与注册互斥。
    /// 条目不随房间删除而移除（房间数量有限），避免锁对象被回收后
    /// 新旧锁对象错位导致的 ABA 竞态。
    /// </summary>
    private readonly ConcurrentDictionary<string, object> _roomLocks = new();

    /// <summary>获取指定房间的锁对象（常驻，不回收）。</summary>
    private object GetRoomLock(string roomId) => _roomLocks.GetOrAdd(roomId, _ => new object());

    /// <summary>
    /// 深拷贝房间配置快照（含 A/B 两方单位列表），
    /// 供外部以只读方式消费，避免绕过房间锁修改 Store 内可变对象。
    /// </summary>
    private static GameRoom CloneRoom(GameRoom source) {
        var copy = new GameRoom(source.RoomId) {
            Title = source.Title,
            DungeonName = source.DungeonName,
            Description = source.Description,
            Category = source.Category,
            HostName = source.HostName,
            MaxPlayers = source.MaxPlayers,
            CurrentPlayers = source.CurrentPlayers,
            Password = source.Password,
            CreatedAt = source.CreatedAt,
            Status = source.Status,
        };
        copy.Units.AddRange(source.Units);
        return copy;
    }

    // ─── IRoomStateStore ───

    /// <inheritdoc />
    public bool TryRegisterRoom(string roomId, string? password, GameRoom config) {
        lock (GetRoomLock(roomId)) {
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
    }

    /// <inheritdoc />
    public bool TryRegisterRoomWithHost(string roomId, string? password, GameRoom config,
        string hostName, string hostPlayerId, int hostPeerId) {
        lock (GetRoomLock(roomId)) {
            if (!TryRegisterRoom(roomId, password, config))
                return false;

            // 单锁内完成房主登记（复用 RegisterRoomPlayer 的同锁内联逻辑，
            // 避免对外再取一次锁时房间可能已被并发移除）
            _roomHosts[roomId] = hostName;
            var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
            states.TryAdd(hostName, false);
            _peerPlayers[hostPeerId] = (roomId, hostName);
            if (!string.IsNullOrEmpty(hostPlayerId))
                RegisterRoomPlayerId(roomId, hostName, hostPlayerId);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Store] Room '{RoomId}' registered with host '{Host}' (atomic)",
                    roomId, hostName);
            return true;
        }
    }

    /// <inheritdoc />
    public bool RoomExists(string roomId) => _roomConfigs.ContainsKey(roomId);

    /// <inheritdoc />
    public GameRoom? GetRoomConfig(string roomId) {
        lock (GetRoomLock(roomId)) {
            if (!_roomConfigs.TryGetValue(roomId, out var config))
                return null;
            // 深拷贝：阻止调用方绕过房间锁直接改写 Store 内的可变配置对象
            return CloneRoom(config);
        }
    }

    /// <inheritdoc />
    public bool IsRoomMember(string roomId, string playerId) {
        lock (GetRoomLock(roomId)) {
            if (!_roomPlayerIds.TryGetValue(roomId, out var ids))
                return false;
            return ids.Values.Any(id => id == playerId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RoomListing> ListActiveRooms() {
        // 跨房间枚举统一不加锁（避免多锁获取顺序造成死锁），
        // 依赖 ConcurrentDictionary 的弱一致性快照语义；字段可能略旧，可接受。
        return [.. _roomConfigs
            .Where(kvp => kvp.Value.Status != RoomStatus.Finished)
            .Select(kvp => RoomListing.FromGameRoom(kvp.Value))
            .OrderByDescending(r => r.CreatedAt)];
    }

    /// <inheritdoc />
    public void UpdateRoomStatus(string roomId, RoomStatus status) {
        lock (GetRoomLock(roomId)) {
            if (_roomConfigs.TryGetValue(roomId, out var config))
                config.Status = status;
        }
    }

    /// <inheritdoc />
    public void UpdatePlayerCount(string roomId, int count) {
        lock (GetRoomLock(roomId)) {
            if (_roomConfigs.TryGetValue(roomId, out var config))
                config.CurrentPlayers = count;
        }
    }

    /// <inheritdoc />
    public int IncrementPlayerCount(string roomId) {
        lock (GetRoomLock(roomId)) {
            if (_roomConfigs.TryGetValue(roomId, out var config)) {
                config.CurrentPlayers++;
                return config.CurrentPlayers;
            }
            return 0;
        }
    }

    /// <inheritdoc />
    public bool ValidateRoomPassword(string roomId, string? password) {
        lock (GetRoomLock(roomId)) {
            if (!_roomPasswords.TryGetValue(roomId, out var storedPassword))
                return false;
            return storedPassword == null || storedPassword == password;
        }
    }

    /// <inheritdoc />
    public void RemoveRoomState(string roomId) {
        lock (GetRoomLock(roomId)) {
            _roomPasswords.TryRemove(roomId, out _);
            _roomConfigs.TryRemove(roomId, out _);
            _prepareUnits.TryRemove(roomId, out _);
            _roomHosts.TryRemove(roomId, out _);
            _roomReadyStates.TryRemove(roomId, out _);
            _roomPlayerIds.TryRemove(roomId, out _);
            // 清理 peerPlayers 中属于该房间的条目（与 RegisterRoomPlayer 互斥）
            foreach (var kv in _peerPlayers) {
                if (kv.Value.RoomId == roomId)
                    _peerPlayers.TryRemove(kv.Key, out _);
            }
        }
    }

    /// <inheritdoc />
    public void ClearAllState() {
        // 仅在服务端停止流程调用，此时后台线程均已 Join，不存在并发写入。
        _roomPasswords.Clear();
        _roomConfigs.Clear();
        _prepareUnits.Clear();
        _roomHosts.Clear();
        _roomReadyStates.Clear();
        _roomPlayerIds.Clear();
        _peerPlayers.Clear();
        // 锁表随状态一并清空（停机后不再有房间，旧锁对象无保留价值）
        _roomLocks.Clear();
    }

    /// <summary>
    /// 释放存储（装配层使用 using 管理生命周期时调用）。
    /// 与停机流程等价：清空全部状态；调用方应确保后台线程已 Join
    /// （GameServer.Stop/GameLobby.StopAll 已保证）。
    /// </summary>
    public void Dispose() => ClearAllState();

    // ─── IPlayerStateStore ───

    /// <inheritdoc />
    public void SetRoomHost(string roomId, string hostName) {
        lock (GetRoomLock(roomId)) {
            _roomHosts[roomId] = hostName;
            // 将房主登记为房间成员（准备状态默认未准备，房主的准备状态不参与全员判定）
            var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
            states.TryAdd(hostName, false);
        }
    }

    /// <inheritdoc />
    public void RegisterRoomPlayer(string roomId, string playerName, string playerId, int peerId) {
        lock (GetRoomLock(roomId)) {
            var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
            states.TryAdd(playerName, false);
            _peerPlayers[peerId] = (roomId, playerName);
            RegisterRoomPlayerId(roomId, playerName, playerId);
        }
    }

    /// <inheritdoc />
    public void RegisterRoomPlayerId(string roomId, string playerName, string playerId) {
        if (string.IsNullOrEmpty(playerId))
            return;

        lock (GetRoomLock(roomId)) {
            var ids = _roomPlayerIds.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, string>());
            ids[playerName] = playerId;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetRoomPlayerIds(string roomId) {
        lock (GetRoomLock(roomId)) {
            if (_roomPlayerIds.TryGetValue(roomId, out var ids))
                return new Dictionary<string, string>(ids);
            return [];
        }
    }

    /// <inheritdoc />
    public void SetPlayerReady(string roomId, string playerName, bool ready) {
        lock (GetRoomLock(roomId)) {
            if (_roomHosts.TryGetValue(roomId, out var hostName) && hostName == playerName)
                return;

            if (_roomReadyStates.TryGetValue(roomId, out var states))
                states[playerName] = ready;
        }
    }

    /// <inheritdoc />
    public bool IsAllOthersReady(string roomId) {
        lock (GetRoomLock(roomId)) {
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
    }

    /// <inheritdoc />
    public RoomStateSnapshot GetRoomState(string roomId) {
        lock (GetRoomLock(roomId)) {
            string hostName = _roomHosts.TryGetValue(roomId, out var host) ? host : "";
            string dungeonName = _roomConfigs.TryGetValue(roomId, out var config) ? config.DungeonName : "";
            var players = new List<PlayerReadyState>();
            if (_roomReadyStates.TryGetValue(roomId, out var states)) {
                foreach (var kv in states)
                    players.Add(new PlayerReadyState(kv.Key, kv.Value));
            }
            return new RoomStateSnapshot(hostName, dungeonName, players);
        }
    }

    /// <inheritdoc />
    public bool IsPeerRoomHost(int peerId, string roomId) {
        lock (GetRoomLock(roomId)) {
            if (!_peerPlayers.TryGetValue(peerId, out var entry))
                return false;
            if (entry.RoomId != roomId)
                return false;
            return _roomHosts.TryGetValue(roomId, out var host) && entry.PlayerName == host;
        }
    }

    /// <inheritdoc />
    public string? GetPlayerNameForPeer(int peerId) {
        // 单条目 TryGetValue 由 ConcurrentDictionary 保证原子性，
        // 无需房间锁；读到的归属可能略旧，但 peer 归属仅作身份校验用。
        if (_peerPlayers.TryGetValue(peerId, out var entry))
            return entry.PlayerName;
        return null;
    }

    /// <inheritdoc />
    public string? RemovePlayerByPeer(int peerId) {
        // 锁外先取房间用于定位锁对象（弱一致性，允许）
        if (!_peerPlayers.TryGetValue(peerId, out var entry))
            return null;

        lock (GetRoomLock(entry.RoomId)) {
            // 锁内重新确认，避免 peerId 已被并发重新归属到其他房间
            if (!_peerPlayers.TryRemove(peerId, out var current))
                return null;

            if (_roomReadyStates.TryGetValue(current.RoomId, out var states)) {
                states.TryRemove(current.PlayerName, out _);
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("[Store] Player '{Player}' removed from room '{RoomId}' (disconnected)",
                        current.PlayerName, current.RoomId);
            }
            return current.RoomId;
        }
    }

    /// <inheritdoc />
    public bool AddPrepareUnit(string roomId, string unitName, string camp, string playerName, string playerId) {
        lock (GetRoomLock(roomId)) {
            if (!_prepareUnits.TryGetValue(roomId, out var units))
                return false;

            units.Add((unitName, camp, playerName, playerId));
            return true;
        }
    }

    /// <inheritdoc />
    public bool RemovePrepareUnit(string roomId, string unitName, string camp) {
        lock (GetRoomLock(roomId)) {
            if (!_prepareUnits.TryGetValue(roomId, out var units))
                return false;
            return units.RemoveAll(u => u.UnitName == unitName && u.Camp == camp) > 0;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<UnitSelection> GetPrepareUnits(string roomId) {
        lock (GetRoomLock(roomId)) {
            if (!_prepareUnits.TryGetValue(roomId, out var units))
                return [];
            return [.. units.Select(u => new UnitSelection(u.UnitName, u.Camp, u.PlayerName, u.PlayerId))];
        }
    }
}
