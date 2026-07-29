using Godot;
using System;
using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle;

/// <summary>
/// 阵营起始点区域。根据阵营 (CampA / CampB) 提供不同的出生点采样。
/// </summary>
public partial class CampStartPoints : Node3D {
    [Export]
    private float _radius = 4.0f;

    [Export]
    private EnumCamp _camp = EnumCamp.Camp_A;

    private readonly Random _random = new();

    public EnumCamp Camp => _camp;

    /// <summary>
    /// 在起始区域内随机采样一个位置。
    /// </summary>
    public Vector3 SamplePosition() {
        float angle = _random.NextSingle() * 2 * Mathf.Pi;
        float radius = _radius * Mathf.Sqrt(_random.NextSingle());

        float x = radius * Mathf.Cos(angle);
        float z = radius * Mathf.Sin(angle);

        return new Vector3(x, 0, z) + GlobalPosition;
    }
}
