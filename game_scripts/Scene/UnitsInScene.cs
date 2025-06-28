using Godot;
using Godot.Collections;
using System;


namespace GameLogic {
    [GlobalClass]
    public partial class UnitsInScene : Resource {
        public UnitsInScene() {
            unitsArr = new Array<UnitState>();
        }

        [Export]
        double updateInterval = 1.0;
        [ExportGroup("Runtime Parameters")]
        [Export]
        Array<UnitState> unitsArr;
        public Array<UnitState> UnitsArr => [.. unitsArr];
        public Action<UnitsInScene> OnUnitsChangedEvent;

        [Export]
        double sceneTime = 0.0;
        public double SceneTime => sceneTime;
        [Export]
        double lastUpdateTime = 0.0;

        public void UpdateState(double delta) {
            sceneTime += delta;
            foreach (UnitState unit in unitsArr) {
                unit.UpdateState(delta);
            }

            if (lastUpdateTime + updateInterval < sceneTime) {
                lastUpdateTime += updateInterval;
                foreach (UnitState unit in unitsArr) {
                    unit.UpdateStateInterval(updateInterval);
                }
            }
        }

        public void AddUnit(UnitState unitState) {
            unitsArr.Add(unitState);
            OnUnitsChangedEvent?.Invoke(this);
        }

        public void RemoveUnit(UnitState unitState) {
            unitsArr.Remove(unitState);
            OnUnitsChangedEvent?.Invoke(this);
        }

    }
}
