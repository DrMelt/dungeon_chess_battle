using System.Numerics;
using DungeonChessBattle.Battle.Shared.Movement;

namespace DungeonChessBattle.Battle.Logic.Movement;

/// <summary>
/// 确定性移动管线：位置 + 方向 + 速度 + 时间 + 场景交互 → 最终位置。
/// 纯函数、确定性，客户端预测与服务端权威共用同一实现。
/// 移动规则，含归一化、推挤与边界，统一在此层，Pawn 只做状态落点。
/// </summary>
public static class MovementResolver {
    /// <summary>
    /// 结算一次移动：先归一化方向并按速度推进，再经场景交互推挤，含边界、静态障碍与单位互斥。
    /// </summary>
    /// <param name="pos">当前位置。</param>
    /// <param name="moveDir">移动方向向量，无需单位化。</param>
    /// <param name="speed">移动速度。</param>
    /// <param name="dt">逻辑帧间隔，秒。</param>
    /// <param name="bodyRadius">单位碰撞半径，供场景交互判定。</param>
    /// <param name="scene">场景交互上下文；为 null 时视为自由移动。</param>
    /// <param name="actorId">移动单位唯一键，场景单位互斥判定排除自身。</param>
    /// <returns>场景交互后的最终位置。</returns>
    public static Vector2 Move(Vector2 pos, Vector2 moveDir, float speed, float dt,
        float bodyRadius, IMovementScene? scene, ushort actorId) {
        if (moveDir.LengthSquared() <= 0.0001f || speed <= 0f || dt <= 0f)
            return pos;

        var dir = moveDir / moveDir.Length(); // 已判非零，防除零
        var delta = dir * speed * dt;
        return scene?.ResolveMove(actorId, pos, delta, bodyRadius) ?? pos + delta;
    }
}
