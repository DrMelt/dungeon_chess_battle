using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Range;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class RangeResBaseGodot : Resource, IRangeRes {
    protected IRangeRes _model = null!;

    private void EnsureModelCreated() {
        if (_model != null)
            return;

        _model = CreateModel();
    }

    protected virtual IRangeRes CreateModel() {
        return new CircularRangeRes();
    }

    public virtual bool IsInRange(IUnitState callSkillObject, IUnitState testObject, System.Numerics.Vector3 targetPos) {
        EnsureModelCreated();
        return _model.IsInRange(callSkillObject, testObject, targetPos);
    }
}
