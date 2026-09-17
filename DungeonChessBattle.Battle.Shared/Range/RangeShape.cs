using System.Numerics;

namespace DungeonChessBattle.Battle.Shared.Range;

/// <summary>
/// 几何范围形状，纯几何判定，不依赖战斗实体。使用 XZ 平面俯视坐标。
/// </summary>
public interface IRangeShape {
    /// <summary>判断检测点是否处于以锚点为基准、给定朝向的范围内。</summary>
    bool Contains(Vector2 point, Vector2 anchor, Vector2 direction, float bodyRadius);

    /// <summary>该形状沿朝向的最远有效判定距离，供自治决策逼近读取。</summary>
    float FarReach {
        get;
    }
}
