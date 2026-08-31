using System.Numerics;

namespace DungeonChessBattle.Battle.Shared.Movement;

/// <summary>
/// 移动结算契约：接收本帧全部单位的移动意图，一次批量解算并返回与输入同序的最终位置。
/// 在线与回放共用同一接口与实现，从同一关卡布局构建以保证确定性一致。
/// 解算只读取传入的 <see cref="MoveIntent"/> 快照，不读取单位可变状态；
/// 避让为就地迭代，结果取决于传入顺序，故调用方须保证三端意图序一致。
/// </summary>
public interface IMovementScene {
    /// <summary>
    /// 批量结算一次移动：对全部意图做单位互斥、静态障碍推挤与竞技场边界约束。
    /// 意图集合外的单位不参与互斥，既不被推开也不构成他人障碍。
    /// </summary>
    /// <param name="intents">本帧全部移动意图，顺序即解算顺序。</param>
    /// <param name="dt">逻辑帧间隔，秒。</param>
    /// <returns>与 intents 同序的最终位置。</returns>
    IReadOnlyList<Vector2> Resolve(IReadOnlyList<MoveIntent> intents, float dt);
}
