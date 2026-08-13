using System.Collections.Concurrent;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Protocol.Dtos;
using DungeonChessBattle.Server.StateStore.Abstractions;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.StateStore;

/// <summary>
/// 基于进程内 ConcurrentDictionary 的状态存储实现。
/// 收敛大厅级房间状态、玩家状态与准备单位数据的存储逻辑。
/// </summary>
public sealed class InMemoryGameStateStore(ILoggerFactory loggerFactory) : IGameStateStore, IDisposable {
    private readonly ILogger<InMemoryGameStateStore> _logger = loggerFactory.CreateLogger<InMemoryGameStateStore>();

    /// <summary>房间配置注册表，招募板使用。</summary>
    private readonly ConcurrentDictionary<string, GameRoom> _roomConfigs = new();

    /// <summary>房间密码字典。null 表示无密码房间。</summary>
    private readonly ConcurrentDictionary<string, string?> _roomPasswords = new();

    /// <summary>房主玩家名表：房间 ID 到房主 displayName。</summary>
    private readonly ConcurrentDictionary<string, string> _roomHosts = new();

    /// <summary>玩家准备状态表：房间 ID 到玩家名与是否已准备的映射。</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _roomReadyStates = new();

    /// <summary>房间内玩家的连接归属表：connectionId 到房间 ID 与玩家名的映射。</summary>
    private readonly ConcurrentDictionary<string, (string RoomId, string PlayerName)> _peerPlayers = new();

    /// <summary>房间内玩家的 playerId 映射表：房间 ID 到玩家名与 playerId 的映射。</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _roomPlayerIds = new();

    /// <summary>准备阶段单位数据表：房间 ID 到单位名、阵营、玩家名与玩家 ID 的列表。</summary>
    private readonly ConcurrentDictionary<string, List<(string UnitName, string Camp, string PlayerName, string PlayerId)>> _prepareUnits = new();

    /// <summary>
    /// 房间级锁表：房间ID → 锁对象，串行化同一房间的读改写，保证
    /// List&lt;T&gt; 操作、可变模型字段读改写、peer 归属清理与注册互斥。
    /// 条目不随房间删除而移除，房间数量有限，避免锁对象被回收后
    /// 新旧锁对象错位导致的 ABA 竞态。
    /// </summary>
    private readonly ConcurrentDictionary<string, object> _roomLocks = new();

    /// <summary>获取指定房间的锁对象，常驻，不回收。</summary>
    private object GetRoomLock(string roomId) => _roomLocks.GetOrAdd(roomId, _ => new object());

    /// <summary>
    /// 深拷贝房间配置快照，
    /// 供外部以只读方式消费，避免绕过房间锁修改 Store 内可变对象。
    /// 战斗单位状态由 Logic 层权威持有，不进入 Store 深拷贝。
    /// </summary>
    private static GameRoom CloneRoom(GameRoom source) {
        var copy = new GameRoom(source.RoomId) {
            Title = source.Title,
            DungeonName = source.DungeonName,
            Description = source.Description,
            HostName = source.HostName,
            MaxPlayers = source.MaxPlayers,
            CurrentPlayers = source.CurrentPlayers,
            Password = source.Password,
            CreatedAt = source.CreatedAt,
            Status = source.Status,
        };
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
                _logger.LogInformation("Room '{RoomId}' registered (prepare), HasPassword={HasPwd}, Title={Title}",
                    roomId, password != null, config.Title);
            return true;
        }
    }

    /// <inheritdoc />
    public bool TryRegisterRoomWithHost(string roomId, string? password, GameRoom config,
        string hostName, string hostPlayerId, string hostConnectionId) {
        lock (GetRoomLock(roomId)) {
            if (!TryRegisterRoom(roomId, password, config))
                return false;

            // 单锁内完成房主登记，复用 RegisterRoomPlayer 的同锁内联逻辑，
            // 避免对外再取一次锁时房间可能已被并发移除）
            _roomHosts[roomId] = hostName;
            var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
            states.TryAdd(hostName, false);
            _peerPlayers[hostConnectionId] = (roomId, hostName);
            if (!string.IsNullOrEmpty(hostPlayerId))
                RegisterRoomPlayerId(roomId, hostName, hostPlayerId);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Room '{RoomId}' registered with host '{Host}' (atomic)",
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
        // 跨房间枚举统一不加锁，避免多锁获取顺序造成死锁，
        // 依赖 ConcurrentDictionary 的弱一致性快照语义；字段可能略旧，可接受。
        return [.. _roomConfigs
            .Where(kvp => kvp.Value.Status != RoomStatus.Finished)
            .Select(kvp => new RoomListing {
                RoomId = kvp.Value.RoomId,
                Title = kvp.Value.Title,
                DungeonName = kvp.Value.DungeonName,
                Description = kvp.Value.Description,
                HostName = kvp.Value.HostName,
                CurrentPlayers = kvp.Value.CurrentPlayers,
                MaxPlayers = kvp.Value.MaxPlayers,
                HasPassword = kvp.Value.HasPassword,
                Status = kvp.Value.Status,
                CreatedAt = kvp.Value.CreatedAt,
            })
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
            // 清理 peerPlayers 中属于该房间的条目，与 RegisterRoomPlayer 互斥
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
        // 锁表随状态一并清空，停机后不再有房间，旧锁对象无保留价值
        _roomLocks.Clear();
    }

    /// <summary>
    /// 释放存储，装配层使用 using 管理生命周期时调用。
    /// 与停机流程等价：清空全部状态；调用方应确保后台线程已 Join
    /// GameServer.Stop 与 GameLobby.StopAll 已保证。
    /// </summary>
    public void Dispose() => ClearAllState();

    // ─── IPlayerStateStore ───

    /// <inheritdoc />
    public void SetRoomHost(string roomId, string hostName) {
        lock (GetRoomLock(roomId)) {
            _roomHosts[roomId] = hostName;
            // 将房主登记为房间成员，准备状态默认未准备，房主的准备状态不参与全员判定
            var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
            states.TryAdd(hostName, false);
        }
    }

    /// <inheritdoc />
    public void RegisterRoomPlayer(string roomId, string playerName, string playerId, string connectionId) {
        lock (GetRoomLock(roomId)) {
            var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
            states.TryAdd(playerName, false);
            _peerPlayers[connectionId] = (roomId, playerName);
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
    public bool TrySetPlayerReady(string roomId, string playerName, bool ready) {
        lock (GetRoomLock(roomId)) {
            if (!_roomReadyStates.TryGetValue(roomId, out var states) || !states.ContainsKey(playerName))
                return false;

            // 房主身份不参与准备判定
            if (_roomHosts.TryGetValue(roomId, out var hostName) && hostName == playerName)
                return false;

            // 未选择角色不能准备
            if (ready && !PlayerHasUnitLocked(roomId, playerName))
                return false;

            states[playerName] = ready;
            return true;
        }
    }

    /// <inheritdoc />
    public bool IsPlayerReady(string roomId, string playerName) {
        lock (GetRoomLock(roomId)) {
            return _roomReadyStates.TryGetValue(roomId, out var states)
                && states.TryGetValue(playerName, out var ready) && ready;
        }
    }

    /// <inheritdoc />
    public bool AreAllPlayersUnitSelected(string roomId) {
        lock (GetRoomLock(roomId)) {
            if (!_roomReadyStates.TryGetValue(roomId, out var states) || states.IsEmpty)
                return false;
            if (!_prepareUnits.TryGetValue(roomId, out var units))
                return false;

            foreach (var playerName in states.Keys) {
                if (!PlayerHasUnitLocked(roomId, playerName))
                    return false;
            }
            return true;
        }
    }

    /// <summary>判断房间锁内玩家是否已选择至少一个准备单位。</summary>
    private bool PlayerHasUnitLocked(string roomId, string playerName) {
        if (!_prepareUnits.TryGetValue(roomId, out var units))
            return false;
        return units.Any(u => u.PlayerName == playerName);
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
    public bool IsConnectionRoomHost(string connectionId, string roomId) {
        lock (GetRoomLock(roomId)) {
            if (!_peerPlayers.TryGetValue(connectionId, out var entry))
                return false;
            if (entry.RoomId != roomId)
                return false;
            return _roomHosts.TryGetValue(roomId, out var host) && entry.PlayerName == host;
        }
    }

    /// <inheritdoc />
    public bool IsConnectionInRoom(string connectionId, string roomId) {
        if (_peerPlayers.TryGetValue(connectionId, out var entry))
            return entry.RoomId == roomId;
        return false;
    }

    /// <inheritdoc />
    public string? GetPlayerNameForConnection(string connectionId) {
        // 单条目 TryGetValue 由 ConcurrentDictionary 保证原子性，
        // 无需房间锁；读到的归属可能略旧，但连接归属仅作身份校验用。
        if (_peerPlayers.TryGetValue(connectionId, out var entry))
            return entry.PlayerName;
        return null;
    }

    /// <inheritdoc />
    public string? RemovePlayerByConnection(string connectionId) {
        // 锁外先取房间用于定位锁对象，弱一致性，允许
        if (!_peerPlayers.TryGetValue(connectionId, out var entry))
            return null;

        lock (GetRoomLock(entry.RoomId)) {
            // 锁内重新确认，避免 connectionId 已被并发重新归属到其他房间
            if (!_peerPlayers.TryRemove(connectionId, out var current))
                return null;

            string roomId = current.RoomId;
            string leavingName = current.PlayerName;

            // 移除准备状态、playerId 映射与该玩家的准备单位，任何阶段都执行，避免状态残留
            if (_roomReadyStates.TryGetValue(roomId, out var states))
                states.TryRemove(leavingName, out _);
            if (_roomPlayerIds.TryGetValue(roomId, out var ids))
                ids.TryRemove(leavingName, out _);
            if (_prepareUnits.TryGetValue(roomId, out var units))
                units.RemoveAll(u => u.PlayerName == leavingName);

            // 仅准备阶段房间维护人数、房主转让与空房删除；
            // 战斗中房间的生命周期由 RoomServerManager 全权负责，本方法不触碰。
            if (_roomConfigs.TryGetValue(roomId, out var config) && config.Status == RoomStatus.Waiting) {
                config.CurrentPlayers = Math.Max(0, config.CurrentPlayers - 1);

                // 房主退出：转让给剩余玩家，房主表与招募板配置同步更新，保持一致
                if (_roomHosts.TryGetValue(roomId, out var hostName) && hostName == leavingName) {
                    string? newHost = null;
                    if (states != null) {
                        foreach (var name in states.Keys) {
                            if (name != leavingName) {
                                newHost = name;
                                break;
                            }
                        }
                    }
                    if (newHost != null) {
                        _roomHosts[roomId] = newHost;
                        config.HostName = newHost;
                        if (_logger.IsEnabled(LogLevel.Information))
                            _logger.LogInformation("Host of room '{RoomId}' transferred to '{NewHost}' (old host '{OldHost}' left).",
                                roomId, newHost, leavingName);
                    }
                    else {
                        _roomHosts.TryRemove(roomId, out _);
                    }
                }

                // 最后一人退出：删除房间全部状态
                if (config.CurrentPlayers <= 0) {
                    RemoveRoomState(roomId);
                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation("Room '{RoomId}' removed (last player '{Player}' left).",
                            roomId, leavingName);
                    return null; // 房间已删除，调用方无需广播
                }
            }

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Player '{Player}' removed from room '{RoomId}' (disconnected)",
                    leavingName, roomId);
            return roomId;
        }
    }

    /// <inheritdoc />
    public bool AddPrepareUnit(string roomId, string unitName, string camp, string playerName, string playerId) {
        lock (GetRoomLock(roomId)) {
            if (!_prepareUnits.TryGetValue(roomId, out var units))
                return false;

            // 已准备的玩家不能更改角色
            if (_roomReadyStates.TryGetValue(roomId, out var states)
                && states.TryGetValue(playerName, out var ready) && ready)
                return false;

            units.Add((unitName, camp, playerName, playerId));
            return true;
        }
    }

    /// <inheritdoc />
    public bool RemovePrepareUnit(string roomId, string unitName, string camp, string ownerName) {
        lock (GetRoomLock(roomId)) {
            if (!_prepareUnits.TryGetValue(roomId, out var units))
                return false;

            // 已准备的玩家不能更改角色
            if (_roomReadyStates.TryGetValue(roomId, out var states)
                && states.TryGetValue(ownerName, out var ready) && ready)
                return false;

            return units.RemoveAll(u => u.UnitName == unitName && u.Camp == camp && u.PlayerName == ownerName) > 0;
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
