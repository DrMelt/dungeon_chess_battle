namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 游戏房间数据模型，承载两个阵营的单位列表与战斗状态。
/// </summary>
public class GameRoom(string roomId) {
    public string RoomId {
        get;
    } = roomId;
    public List<UnitModel> UnitsA { get; } = [];
    public List<UnitModel> UnitsB { get; } = [];
    public bool IsActive {
        get; set;
    } = true;
}