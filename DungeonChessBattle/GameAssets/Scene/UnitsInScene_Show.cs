using Godot;
using Godot.Collections;

namespace DungeonChessBattle;

public partial class UnitsInScene_Show : Node {
    readonly UnitsInScene unitsInSceneRes = new();

    public UnitsInScene UnitsInSceneRes => unitsInSceneRes;

    public Array<UnitState> UnitsArr => unitsInSceneRes.UnitsArr;



    public void AddUnitShow(UnitGameShow unitGameShow) {
        unitsInSceneRes.AddUnit(unitGameShow.UnitStateRec);
        AddChild(unitGameShow);
    }
}
