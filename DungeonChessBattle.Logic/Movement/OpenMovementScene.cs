using System.Numerics;

namespace DungeonChessBattle.Logic.Movement;

/// <summary>
/// 默认无碰撞移动场景：自由移动，无阻挡、无边界约束。
/// 作为场景交互的占位实现，后续接入地图/碰撞时替换为具体场景。
/// 无状态，使用单例。
/// </summary>
public sealed class OpenMovementScene : IMovementScene {
    /// <summary>无状态单例。</summary>
    public static readonly OpenMovementScene Instance = new();

    private OpenMovementScene() {
    }

    /// <inheritdoc />
    public bool IsWalkable(Vector2 from, Vector2 to, float bodyRadius) => true;

    /// <inheritdoc />
    public Vector2 ClampToBounds(Vector2 pos, float bodyRadius) => pos;
}
