namespace DungeonChessBattle.Battle.Domain;

/// <summary>
/// 编排层读写房间级战斗状态的通道，依赖倒置，由 Entities 的 BattleRoomEntity 实现。
/// 只读投影继承 <see cref="IReadOnlyBattleRoom"/>，客户端与查询侧只经只读接口读取；
/// 写成员为服务端权威写入口，客户端实例存在但不调用。
/// 房间级战斗状态权威由载体 BattleRoomEntity 承载，战斗世界 BattleScene 直接经本接口读写。
/// </summary>
public interface IBattleRoom : IReadOnlyBattleRoom {
    /// <summary>战斗开始：写入 Running 阶段、未结束与开始时刻。服务端权威，客户端实例存在但不调用。</summary>
    void ProjectBattleStarted();

    /// <summary>战斗结束：写入 Finished 阶段与已结束。服务端权威，客户端实例存在但不调用。</summary>
    void ProjectBattleEnded();
}
