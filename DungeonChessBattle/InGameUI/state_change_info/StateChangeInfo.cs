using Godot;
using DungeonChessBattle.Core.Enums;
using System;

namespace DungeonChessBattle;

public partial class StateChangeInfo : Node {
    static Vector2 WorldToScreenPos(Node node, Vector3 wordPos) {
        var camera3D = node.GetViewport().GetCamera3D();
        var screenPos = camera3D.UnprojectPosition(wordPos);
        return screenPos;
    }

    public StateChangeInfoInterRefs? InterRefs {
        get; private set;
    }

    private StateChangeInfoInterRefs InterRefsOrThrow =>
        InterRefs ?? throw new InvalidOperationException("[StateChangeInfo] InterRefs has not been initialized.");

    TookDamageInfo NewTookDamageInfo =>
        InterRefsOrThrow.TookDamageInfoPackedScene?.Instantiate<TookDamageInfo>()
        ?? throw new InvalidOperationException("[StateChangeInfo] TookDamageInfoPackedScene is not assigned or instantiation failed.");

    BuffChangeInfo NewBuffChangeInfo =>
        InterRefsOrThrow.BuffChangeInfoPackedScene?.Instantiate<BuffChangeInfo>()
        ?? throw new InvalidOperationException("[StateChangeInfo] BuffChangeInfoPackedScene is not assigned or instantiation failed.");

    Godot.Collections.Array<UnitState>? preUnits;

    public override void _Ready() {
        InterRefs = GetNode<StateChangeInfoInterRefs>("StateChangeInfoInterRefs");
        if (InterRefs == null) {
            GD.PrintErr("[StateChangeInfo] StateChangeInfoInterRefs node not found.");
        }
    }

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

    void UnbindWithUnitState(UnitState unitState) {
        unitState.OnBuffAddedEvent -= OnUnitBuffAdded;
        unitState.OnBuffRemovedEvent -= OnUnitBuffRemoved;
        unitState.OnTookDamageEvent -= OnUnitTookDamage;
    }

    void OnUnitBuffAdded(UnitState unitState, BuffBaseGodot buff) {
        BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
        AddChild(buffChangeInfo);
        buffChangeInfo.Init(buff, BuffChangeInfo.Enum_BuffChangeType.Added);
        buffChangeInfo.GlobalPosition = WorldToScreenPos(this, unitState.Position + Vector3.Up * 2.2f);
    }

    void OnUnitBuffRemoved(UnitState unitState, BuffBaseGodot buff) {
        BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
        AddChild(buffChangeInfo);
        buffChangeInfo.Init(buff, BuffChangeInfo.Enum_BuffChangeType.Removed);
        buffChangeInfo.GlobalPosition = WorldToScreenPos(this, unitState.Position + Vector3.Up * 2.2f);
    }

    void OnUnitTookDamage(UnitState unitState, float damage, Enum_DamageType damageType) {
        TookDamageInfo tookDamageInfo = NewTookDamageInfo;
        AddChild(tookDamageInfo);
        var uiSettings = InterRefsOrThrow.UserUISettingsRes
            ?? throw new InvalidOperationException("[StateChangeInfo] UserUISettingsRes is not assigned in InterRefs.");
        tookDamageInfo.Init(damage, damageType, uiSettings);
        tookDamageInfo.GlobalPosition = WorldToScreenPos(this, unitState.Position + Vector3.Up * 2.2f);
    }
}
