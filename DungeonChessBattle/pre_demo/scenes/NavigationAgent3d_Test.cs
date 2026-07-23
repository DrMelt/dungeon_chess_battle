using Godot;

namespace DungeonChessBattle;

public partial class NavigationAgent3d_Test : Node3D {
    [Export]
    Node3D movementTargetRef;

    [Export]
    public float MovementSpeed { get; set; } = 4.0f;
    [Export]
    NavigationAgent3D navigationAgent;
    private float _movementDelta;

    public override void _Ready() {
        navigationAgent.VelocityComputed += OnVelocityComputed;
    }

    private void SetMovementTarget(Vector3 movementTarget) {
        navigationAgent.TargetPosition = movementTarget;
    }

    public override void _PhysicsProcess(double delta) {
        SetMovementTarget(movementTargetRef.GlobalPosition);

        // Do not query when the map has never synchronized and is empty.
        if (NavigationServer3D.MapGetIterationId(navigationAgent.GetNavigationMap()) == 0) {
            return;
        }

        if (navigationAgent.IsNavigationFinished()) {
            return;
        }

        _movementDelta = MovementSpeed * (float)delta;
        Vector3 nextPathPosition = navigationAgent.GetNextPathPosition();
        Vector3 newVelocity = GlobalPosition.DirectionTo(nextPathPosition) * _movementDelta;
        if (navigationAgent.AvoidanceEnabled) {
            navigationAgent.Velocity = newVelocity;
        }
        else {
            OnVelocityComputed(newVelocity);
        }
    }

    private void OnVelocityComputed(Vector3 safeVelocity) {
        GlobalPosition = GlobalPosition.MoveToward(GlobalPosition + safeVelocity, _movementDelta);
    }

}
