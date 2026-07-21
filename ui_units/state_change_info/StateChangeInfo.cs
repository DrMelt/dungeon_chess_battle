using GameLogic;
using Godot;
using System;

public partial class StateChangeInfo : Node {
    static Vector2 WorldToScreenPos(Node node, Vector3 wordPos) {
        var camera3D = node.GetViewport().GetCamera3D();
        var screenPos = camera3D.UnprojectPosition(wordPos);
        return screenPos;
    }

    [Export]
    UserUISettings userUISettingsRes;
    [Export]
    PackedScene _tookDamageInfo_PKS;
    TookDamageInfo NewTookDamageInfo {
        get => _tookDamageInfo_PKS.Instantiate<TookDamageInfo>();
    }
    [Export]
    PackedScene _buffChangeInfo_PKS;
    BuffChangeInfo NewBuffChangeInfo {
        get => _buffChangeInfo_PKS.Instantiate<BuffChangeInfo>();
    }

    Godot.Collections.Array<UnitState> preUnits;

    public void BindUnitsInScene(UnitsInScene unitsInSceneRes) {
        unitsInSceneRes.OnUnitsChangedEvent += OnUnitsInSceneChanged;
    }



    void OnUnitsInSceneChanged(UnitsInScene unitsInScene) {
        if (preUnits != null) {
            foreach (var unit in preUnits) {
                {
                    UnbindWithUnitState(unit);
                }
            }
        }

        Godot.Collections.Array<UnitState> units = unitsInScene.UnitsArr;
        foreach (var unit in units) {
            BindWithUnitState(unit);
        }
        preUnits = units;
    }

    void BindWithUnitState(UnitState unitState) {
        unitState.OnBuffAddedEvent += OnUnitBuffAdded;
        unitState.OnBuffRemovedEvent += OnUnitBuffRemoved;

        unitState.OnTookDamageEvent += OnUnitTookDamage;
    }

    public void UnbindWithUnitState(UnitState unitState) {
        unitState.OnBuffAddedEvent -= OnUnitBuffAdded;
        unitState.OnBuffRemovedEvent -= OnUnitBuffRemoved;

        unitState.OnTookDamageEvent -= OnUnitTookDamage;
    }

    void OnUnitBuffAdded(UnitState unitState, BuffBase buffBase) {
        SpawnInfoNode<BuffChangeInfo>(NewBuffChangeInfo, unitState, Vector3.Up * 0.5f, (info) => {
            info.Init(buffBase, BuffChangeInfo.Enum_BuffChangeType.Added);
        });
    }

    void OnUnitBuffRemoved(UnitState unitState, BuffBase buffBase) {
        SpawnInfoNode<BuffChangeInfo>(NewBuffChangeInfo, unitState, Vector3.Up * 0.5f, (info) => {
            info.Init(buffBase, BuffChangeInfo.Enum_BuffChangeType.Removed);
        });
    }

    void OnUnitTookDamage(UnitState unitState, float damage, Enum_DamageType type) {
        SpawnInfoNode<TookDamageInfo>(NewTookDamageInfo, unitState, Vector3.Up * 0.5f, (info) => {
            info.Init(damage, type, userUISettingsRes);
        });
    }


    T SpawnInfoNode<T>(T newInfoNode, UnitState unitState, Vector3 positionOffset, Action<T> initAction) where T : Control {
        Vector2 screenPos = WorldToScreenPos(this, unitState.Position + positionOffset);
        AddChild(newInfoNode);
        newInfoNode.GlobalPosition = screenPos;
        initAction?.Invoke(newInfoNode);
        return newInfoNode;
    }

}
