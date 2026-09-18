using Godot;

namespace DungeonChessBattle.Game.Mod.Shared;

/// <summary>
/// 矩形范围提示接口：mod 侧提示场景的根节点实现，宿主在选位置目标期间驱动。
/// 实现者须为 Node3D 场景根：宿主把它挂进战斗场景后调用，调用时场景的 _Ready 已完成。
/// </summary>
public interface IRectRangeHint {
    /// <summary>
    /// 按施法起点、指向与矩形范围参数摆放提示，参数取自领域范围形状。
    /// </summary>
    /// <param name="fromPos">施法单位位置。</param>
    /// <param name="toPos">目标指向位置。</param>
    /// <param name="near">近端距离。</param>
    /// <param name="far">远端距离。</param>
    /// <param name="fromLeft">矩形左边界。</param>
    /// <param name="toRight">矩形右边界。</param>
    void Init(Vector3 fromPos, Vector3 toPos, float near, float far, float fromLeft, float toRight);
}
