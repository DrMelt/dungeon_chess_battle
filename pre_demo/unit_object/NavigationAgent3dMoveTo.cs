using Godot;

namespace DungeonChessBattle {

    public partial class NavigationAgent3dMoveTo : NavigationAgent3D {
        [Export]
        UnitGameShow unitShowRef;

        Vector3? targetPos = null;
        public Vector3? TargetPos {
            get => targetPos; set => targetPos = value;
        }

        private float _movementDelta;


        public override void _Ready() {
            VelocityComputed += OnVelocityComputed;
            TargetReached += OnTargetReached;

        }

        private void OnTargetReached() {
            unitShowRef.SetUnitGlobalPosition((Vector3)targetPos);
            targetPos = null;
        }

        public override void _PhysicsProcess(double delta) {
            if (targetPos == null) {
                return;
            }

            TargetPosition = targetPos.Value;

            // Do not query when the map has never synchronized and is empty.
            if (NavigationServer3D.MapGetIterationId(GetNavigationMap()) == 0) {
                return;
            }

            if (IsNavigationFinished()) {
                return;
            }

            unitShowRef.GlobalPosition = unitShowRef.UnitStateRec.Position;

            _movementDelta = unitShowRef.UnitStateRec.MoveSpeed * (float)delta;
            Vector3 nextPathPosition = GetNextPathPosition();
            Vector3 newVelocity = unitShowRef.GlobalPosition.DirectionTo(nextPathPosition) * _movementDelta;
            if (AvoidanceEnabled) {
                Velocity = newVelocity;
            }
            else {
                OnVelocityComputed(newVelocity);
            }
        }

        private void OnVelocityComputed(Vector3 safeVelocity) {
            Vector3 pos = unitShowRef.GlobalPosition;
            var newPos = pos.MoveToward(pos + safeVelocity, _movementDelta);
            unitShowRef.SetUnitGlobalPosition(newPos);
            unitShowRef.SetUnitGlobalDir(safeVelocity.Normalized());
        }


    }

}
