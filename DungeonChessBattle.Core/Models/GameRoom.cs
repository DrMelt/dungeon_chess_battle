using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 游戏房间数据模型，承载两个阵营的单位列表与战斗状态。
/// </summary>
public class GameRoom(string roomId) {
    public string RoomId {
        get;
    } = roomId;
    public List<IUnitState> UnitsA { get; } = [];
    public List<IUnitState> UnitsB { get; } = [];
    public bool IsActive {
        get; set;
    } = true;
}
