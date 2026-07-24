using System.Text.Json;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Server.Network;

namespace DungeonChessBattle.Server.Handlers;

/// <summary>
/// 网络消息处理器，解析客户端 JSON 消息并路由到 Logic 层。
/// 维护 peerId → roomId 映射，支持多房间多客户端并发。
/// </summary>
public class GameMessageHandler {
    private readonly GameLogicService _logicService = new();
    private readonly ServerNetworkManager _networkManager;
    private readonly Dictionary<int, string> _peerRooms = [];  // peerId → roomId

    public GameMessageHandler(ServerNetworkManager networkManager) {
        _networkManager = networkManager;
    }

    /// <summary>
    /// 处理客户端连接的清理工作。
    /// </summary>
    public void OnClientConnected(int peerId) {
        Console.WriteLine($"[Game] Client {peerId} connected.");
    }

    /// <summary>
    /// 处理客户端断开时的清理工作。
    /// </summary>
    public void OnClientDisconnected(int peerId) {
        Console.WriteLine($"[Game] Client {peerId} disconnected.");
        _peerRooms.Remove(peerId);
    }

    /// <summary>
    /// 处理客户端发来的消息，解析 Method 并路由到对应 Logic 方法。
    /// </summary>
    public void HandleMessage(int peerId, string json) {
        try {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Method", out var methodProp))
                return;

            string method = methodProp.GetString()!;
            int requestId = root.TryGetProperty("RequestId", out var reqProp) ? reqProp.GetInt32() : -1;

            object? result = method switch {
                "CreateRoom" => HandleCreateRoom(peerId, root),
                "GetRoom" => HandleGetRoom(root),
                "RemoveRoom" => HandleRemoveRoom(peerId, root),
                "StartBattleInRoom" => HandleStartBattle(peerId, root),
                "GetBattle" => HandleGetBattle(root),
                "AdvancePhase" => HandleAdvancePhase(root),
                "NextRound" => HandleNextRound(root),
                "EndBattle" => HandleEndBattle(root),
                "CastSkill" => HandleCastSkill(peerId, root),
                "UpdateBuffs" => HandleUpdateBuffs(peerId, root),
                "CheckBattleEnded" => HandleCheckBattleEnded(root),
                _ => $"Unknown method: {method}"
            };

            // 如果有 RequestId，发送响应回客户端
            if (requestId >= 0 && result != null) {
                _networkManager.SendResponse(peerId, requestId, result);
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"[Game] Error handling message from {peerId}: {ex.Message}");
        }
    }

    #region 消息处理方法

    private string HandleCreateRoom(int peerId, JsonElement root) {
        var roomId = GetArg<string>(root, 0)
            ?? throw new InvalidOperationException("RoomId required.");
        var room = _logicService.CreateRoom(roomId);
        _peerRooms[peerId] = roomId;
        return JsonSerializer.Serialize(new { RoomId = room.RoomId, IsActive = room.IsActive });
    }

    private string HandleGetRoom(JsonElement root) {
        var roomId = GetArg<string>(root, 0);
        var room = _logicService.GetRoom(roomId!);
        return room == null ? "null" : JsonSerializer.Serialize(new { RoomId = room.RoomId, IsActive = room.IsActive });
    }

    private string HandleRemoveRoom(int peerId, JsonElement root) {
        var roomId = GetArg<string>(root, 0)
            ?? throw new InvalidOperationException("RoomId required.");
        bool ok = _logicService.RemoveRoom(roomId);
        _peerRooms.Remove(peerId);
        return JsonSerializer.Serialize(ok);
    }

    private string HandleStartBattle(int peerId, JsonElement root) {
        var roomId = GetRoomId(peerId, root);
        var battle = _logicService.StartBattleInRoom(roomId);
        return JsonSerializer.Serialize(new { Round = battle.RoundNumber, Phase = battle.CurrentPhase.ToString() });
    }

    private string HandleGetBattle(JsonElement root) {
        var roomId = GetArg<string>(root, 0);
        var battle = _logicService.GetBattle(roomId!);
        return battle == null ? "null" : JsonSerializer.Serialize(new { Round = battle.RoundNumber, Phase = battle.CurrentPhase.ToString() });
    }

    private string HandleAdvancePhase(JsonElement root) {
        var roomId = GetArg<string>(root, 0);
        var battle = GetBattleForRoom(roomId!);
        _logicService.AdvancePhase(battle);
        return JsonSerializer.Serialize(battle.CurrentPhase.ToString());
    }

    private string HandleNextRound(JsonElement root) {
        var roomId = GetArg<string>(root, 0);
        var battle = GetBattleForRoom(roomId!);
        _logicService.NextRound(battle);
        return JsonSerializer.Serialize(new { Round = battle.RoundNumber, Phase = battle.CurrentPhase.ToString() });
    }

    private string HandleEndBattle(JsonElement root) {
        var roomId = GetArg<string>(root, 0);
        var battle = GetBattleForRoom(roomId!);
        _logicService.EndBattle(battle);
        return JsonSerializer.Serialize("Finished");
    }

    private string HandleCastSkill(int peerId, JsonElement root) {
        var roomId = GetRoomId(peerId, root);
        var battle = GetBattleForRoom(roomId);

        // 从 Payload 中解析技能数据（简化处理：直接序列化结果）
        if (root.TryGetProperty("Payload", out var payload)) {
            Console.WriteLine($"[Game] CastSkill in room {roomId}: {payload}");
            // TODO: 完整实现需要从 UnitModel 池中查找 caster/target
            // 当前先记录日志并返回确认
        }

        return JsonSerializer.Serialize(new { Handled = true, RoomId = roomId });
    }

    private string HandleUpdateBuffs(int peerId, JsonElement root) {
        // Buff 更新由服务端 Tick 主导，客户端请求仅作确认
        return JsonSerializer.Serialize(new { Handled = true });
    }

    private string HandleCheckBattleEnded(JsonElement root) {
        var roomId = GetArg<string>(root, 0);
        var room = _logicService.GetRoom(roomId!)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");
        bool ended = _logicService.CheckBattleEnded(room);
        return JsonSerializer.Serialize(ended);
    }

    #endregion

    #region 辅助方法

    private string GetRoomId(int peerId, JsonElement root) {
        return GetArg<string>(root, 0)
            ?? (_peerRooms.TryGetValue(peerId, out var rid) ? rid : null!)
            ?? throw new InvalidOperationException("RoomId not found and peer not associated with any room.");
    }

    private BattleManager GetBattleForRoom(string roomId) {
        return _logicService.GetBattle(roomId)
            ?? throw new InvalidOperationException($"No active battle in room {roomId}. Start battle first.");
    }

    private T? GetArg<T>(JsonElement root, int index) {
        if (!root.TryGetProperty("Args", out var argsProp))
            return default;
        var args = argsProp.EnumerateArray().ToList();
        if (index >= args.Count)
            return default;
        var element = args[index];
        return JsonSerializer.Deserialize<T>(element.GetRawText());
    }

    #endregion
}