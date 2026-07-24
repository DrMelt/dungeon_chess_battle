using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Rooms;
using DungeonChessBattle.Logic.Services;

namespace DungeonChessBattle.Server.Handlers;

/// <summary>
/// 网络消息处理器，将客户端请求翻译为 Logic 层调用，结果回写实体同步。
/// </summary>
public class GameMessageHandler
{
    private readonly GameLogicService _logicService = new();

    public GameRoom CreateRoom(string roomId)
    {
        return _logicService.CreateRoom(roomId);
    }

    public BattleManager StartBattle(string roomId)
    {
        return _logicService.StartBattleInRoom(roomId);
    }

    public void HandleCastSkill(string roomId, UnitModel caster, UnitModel target, SkillModel skill)
    {
        var room = _logicService.GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");

        var battle = new BattleManager();
        _logicService.CastSkill(battle, caster, target, skill);
    }
}