using System.Numerics;

namespace DungeonChessBattle.Battle.Domain.Movement;

/// <summary>
/// 移动结算契约：从当前位置施加位移，返回经场景推挤后的最终位置。
/// 客户端预测与服务端权威共用同一接口，从同一关卡配置构建以保证确定性一致。
/// 仅描述结算行为，参与者生命周期由 <see cref="IActorRegistration"/> 承载。
/// </summary>
public interface IMovementScene {
    /// <summary>
    /// 从 from 施加 delta 位移，返回经竞技场边界与静态障碍推挤后的最终位置。
    /// 无互斥能力的场景忽略 actorId，仅做自由移动。
    /// </summary>
    /// <param name="actorId">移动中的演员唯一键，通常为 Pawn 网络实体 ID。</param>
    /// <param name="from">当前位置。</param>
    /// <param name="delta">本帧位移向量。</param>
    /// <param name="bodyRadius">移动演员碰撞半径。</param>
    /// <returns>推挤后的最终位置。</returns>
    Vector2 ResolveMove(ushort actorId, Vector2 from, Vector2 delta, float bodyRadius);
}

/// <summary>
/// 移动场景的参与者注册契约。半径与位置提供器在每次结算时实时读取权威状态，
/// 客户端远程单位位置来自服务端同步并在回滚时还原，场景不维护动态副本。
/// </summary>
public interface IActorRegistration {
    /// <summary>注册一个参与单位互斥的演员。</summary>
    /// <param name="actorId">演员唯一键，通常为 Pawn 网络实体 ID。</param>
    /// <param name="radiusProvider">演员碰撞半径提供器。</param>
    /// <param name="positionProvider">演员当前位置提供器。</param>
    void AddActor(ushort actorId, Func<float> radiusProvider, Func<Vector2> positionProvider);

    /// <summary>移除演员，不再参与单位互斥。</summary>
    /// <param name="actorId">演员唯一键。</param>
    void RemoveActor(ushort actorId);
}
