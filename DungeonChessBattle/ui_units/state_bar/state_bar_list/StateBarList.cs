using DungeonChessBattle.Core;
using DungeonChessBattle.Core.Enums;
using Godot;

namespace DungeonChessBattle;

public partial class StateBarList : Control {
    [Export]
    EnumCamp listOfCamp;

    [ExportGroup("Internal")]
    [Export]
    VBoxContainer vBoxContainerRef;

    [Export]
    PackedScene stateBarMini_PKS;
    StateBarMini NewStateBarMini => stateBarMini_PKS.Instantiate<StateBarMini>();


    UnitsInScene bindingUnitsInScene;


    public void BindUnitsInScene(UnitsInScene unitsInScene) {
        bindingUnitsInScene?.OnUnitsChangedEvent -= OnUnitsChanged;
        bindingUnitsInScene = unitsInScene;

        bindingUnitsInScene.OnUnitsChangedEvent += OnUnitsChanged;
        OnUnitsChanged(bindingUnitsInScene);
    }

    void OnUnitsChanged(UnitsInScene scene) {
        var children = vBoxContainerRef.GetChildren();
        foreach (var child in children) {
            child.QueueFree();
        }

        var units = scene.UnitsArr;
        foreach (var unit in units) {
            if (unit.Camp == listOfCamp) {
                StateBarMini stateBarMini = NewStateBarMini;

                vBoxContainerRef.AddChild(stateBarMini);
                stateBarMini.BindUnitState(unit);
            }
        }
    }

}
