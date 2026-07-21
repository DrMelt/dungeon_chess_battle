using GameLogic;
using Godot;

namespace DungeonChessBattle {

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

        void UnbindWithUnitState(UnitState unitState) {
            unitState.OnBuffAddedEvent -= OnUnitBuffAdded;
            unitState.OnBuffRemovedEvent -= OnUnitBuffRemoved;
            unitState.OnTookDamageEvent -= OnUnitTookDamage;
        }

        void OnUnitBuffAdded(UnitState unitState, BuffBase buff) {
            BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
            AddChild(buffChangeInfo);
            buffChangeInfo.Init(buff, BuffChangeInfo.Enum_BuffChangeType.Added);
            buffChangeInfo.GlobalPosition = WorldToScreenPos(this, unitState.Position + Vector3.Up * 2.2f);
        }

        void OnUnitBuffRemoved(UnitState unitState, BuffBase buff) {
            BuffChangeInfo buffChangeInfo = NewBuffChangeInfo;
            AddChild(buffChangeInfo);
            buffChangeInfo.Init(buff, BuffChangeInfo.Enum_BuffChangeType.Removed);
            buffChangeInfo.GlobalPosition = WorldToScreenPos(this, unitState.Position + Vector3.Up * 2.2f);
        }

        void OnUnitTookDamage(UnitState unitState, float damage, Enum_DamageType damageType) {
            TookDamageInfo tookDamageInfo = NewTookDamageInfo;
            AddChild(tookDamageInfo);
            tookDamageInfo.Init(damage, damageType, userUISettingsRes);
            tookDamageInfo.GlobalPosition = WorldToScreenPos(this, unitState.Position + Vector3.Up * 2.2f);
        }
    }
}
