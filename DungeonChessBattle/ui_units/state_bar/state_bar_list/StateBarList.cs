using Godot;

namespace DungeonChessBattle;

public partial class StateBarList : Control {
    public StateBarListInterRefs? InterRefs {
        get; private set;
    }

    public override void _Ready() {
        InterRefs = GetNode<StateBarListInterRefs>("StateBarListInterRefs");
    }

    StateBarMini NewStateBarMini => InterRefs!.StateBarMiniPKS!.Instantiate<StateBarMini>();

    UnitsInScene bindingUnitsInScene = null!;

    public void BindUnitsInScene(UnitsInScene unitsInScene) {
        bindingUnitsInScene?.OnUnitsChangedEvent -= OnUnitsChanged;
        bindingUnitsInScene = unitsInScene;

        bindingUnitsInScene.OnUnitsChangedEvent += OnUnitsChanged;
        OnUnitsChanged(bindingUnitsInScene);
    }

    void OnUnitsChanged(UnitsInScene scene) {
        if (InterRefs?.VBoxContainerRef == null)
            return;
        var children = InterRefs.VBoxContainerRef.GetChildren();
        foreach (var child in children) {
            child.QueueFree();
        }

        var units = scene.UnitsArr;
        foreach (var unit in units) {
            if (unit.Camps.Contains(InterRefs.ListOfCamp)) {
                StateBarMini stateBarMini = NewStateBarMini;

                InterRefs.VBoxContainerRef.AddChild(stateBarMini);
                stateBarMini.BindUnitState(unit);
            }
        }
    }

}
