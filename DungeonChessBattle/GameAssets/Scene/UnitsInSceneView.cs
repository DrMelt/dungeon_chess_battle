using Godot;
using Godot.Collections;

namespace DungeonChessBattle;

/// <summary>
/// 场景单位视图节点，持有场景单位集合资源并管理单位视图的挂载。
/// </summary>
public partial class UnitsInSceneView : Node {
    /// <summary>场景单位集合资源。</summary>
    public UnitsInScene UnitsInSceneRes { get; } = new();

    /// <summary>场景单位状态数组快照。</summary>
    public Array<UnitState> UnitsArr => UnitsInSceneRes.UnitsArr;

    /// <summary>
    /// 添加单位视图：注册单位状态并挂载视图节点。
    /// </summary>
    /// <param name="unitGameShow">单位视图实例。</param>
    public void AddUnitShow(UnitGameShow unitGameShow) {
        UnitsInSceneRes.AddUnit(unitGameShow.UnitStateRec);
        AddChild(unitGameShow);
    }
}
