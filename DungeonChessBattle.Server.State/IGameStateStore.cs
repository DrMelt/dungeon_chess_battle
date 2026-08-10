namespace DungeonChessBattle.Server.State;

/// <summary>
/// 服务器状态存储门面：组合房间状态与玩家状态子接口。
/// 网络连接密钥（LobbyNetworkServer）与战斗房间会话（BattleRoomServer）
/// 分别属于网络层与战斗房间的私有所有权，不纳入本门面。
/// 并发语义：任何线程都可安全调用；同一房间内的读改写由实现保证原子性。
/// </summary>
public interface IGameStateStore : IRoomStateStore, IPlayerStateStore;
