using Godot;
using System;
using System.Collections.Generic;

#nullable disable

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
        public bool isAlive = true;

        [ExportGroup("Runtime Parameters")]
        public IUnitState fromUnit;

        public void Update(double deltaTime, IUnitState unitState) {
            if (!isAlive) {
                return;
            }

            ActionDuration(deltaTime, unitState);

            duration -= deltaTime;
            if (duration < 0 || superpositions <= 0) {
                ActionEnd(unitState);
                isAlive = false;
            }
        }

        protected virtual void ActionDuration(double deltaTime, IUnitState unitState) {
        }

        protected virtual void ActionEnd(IUnitState unitState) {
        }

        public void AddSuperpositions(BuffBase buffBase) {
            superpositions += 1;
            superpositions = Mathf.Min(superpositions, buffBase.maxSuperpositions);
            duration = Mathf.Max(duration, buffBase.duration);
        }
    }
}
