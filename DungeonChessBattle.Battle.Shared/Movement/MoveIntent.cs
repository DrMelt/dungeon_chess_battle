using System.Numerics;

namespace DungeonChessBattle.Battle.Shared.Movement;

/// <summary>
/// 单位本帧移动意图视图：领域层组装、场景批量解算的只读输入。
/// 意图描述单位想怎么动，与场景如何推挤避让解耦；解算只读此快照，不读单位可变状态。
/// </summary>
/// <param name="ActorId">移动演员唯一键，场景障碍推挤与结果回写定位用。</param>
/// <param name="FromPosition">本帧起始位置。</param>
/// <param name="Direction">归一化移动方向。</param>
/// <param name="Speed">移动速度。</param>
/// <param name="BodyRadius">碰撞半径，供互斥与障碍推挤判定。</param>
public readonly record struct MoveIntent(
    ushort ActorId,
    Vector2 FromPosition,
    Vector2 Direction,
    float Speed,
    float BodyRadius);
