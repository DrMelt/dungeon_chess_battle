namespace DungeonChessBattle.Server.Abstractions;

/// <summary>
/// 战斗房间服务器生命周期契约，协调抽象。
/// 只暴露原语类型与字符串，不暴露 BattleRoomServer 等实现细节，
/// 使大厅协调层与战斗实现层互不依赖。
/// 实现由 Server.Battle 的 BattleRoomManager 承担。
/// </summary>
public interface IBattleRoomManager {
    /// <summary>开始战斗：创建房间服务器并等待首帧初始化完成，返回房间监听端口。</summary>
    int StartRoomBattle(string roomId);

    /// <summary>获取战斗中房间的监听端口；非战斗中的房间返回 false。</summary>
    bool TryGetRoomPort(string roomId, out int port);

    /// <summary>预注册玩家到房间，断线重连身份校验与命名用。</summary>
    void RegisterPlayer(string roomId, string playerId, string playerName);

    /// <summary>更新已注册玩家的显示名，重连时可能更改。</summary>
    void UpdatePlayerName(string roomId, string playerId, string playerName);

    /// <summary>消费空房间投递队列并执行房间移除，由协调循环周期调用。</summary>
    void ProcessPendingRoomCleanups();

    /// <summary>停止并清空全部房间服务器与大厅状态。</summary>
    void StopAll();

    /// <summary>输出所有房间基本信息，控制台命令 rooms 用。</summary>
    void ListRooms();
}
