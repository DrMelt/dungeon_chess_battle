using Godot;
using System;
using System.Collections.Generic;
using System.Data;

namespace GameLogic {

    [GlobalClass]
    public partial class BuffBase : Resource {
        [Export]
        public string buffName;

        [Export(PropertyHint.MultilineText)]
        public string buffDescription;

        [Export]
        public Texture2D icon;

        [Export]
        public double duration = 60;
        [Export]
        public int superpositions = 1;
        [Export]
        public int maxSuperpositions = 1;
        [Export]
        public bool isActive = true;

        [ExportGroup("Runtime Parameters")]
        [Export]
        public UnitState fromUnit;


        public void Update(double deltaTime, UnitState unitState) {
            if (!isActive) {
                return;
            }

            ActionDuration(deltaTime, unitState);

            duration -= deltaTime;
            if (duration < 0 || superpositions <= 0) {
                ActionEnd(unitState);
                isActive = false;
            }
        }

        protected virtual void ActionDuration(double deltaTime, UnitState unitState) {

        }

        protected virtual void ActionEnd(UnitState unitState) {

        }

        internal void AddSuperpositions(BuffBase buffBase) {
            superpositions += 1;
            superpositions = Mathf.Min(superpositions, buffBase.maxSuperpositions);
            duration = Mathf.Max(duration, buffBase.duration);
        }

    }
}
