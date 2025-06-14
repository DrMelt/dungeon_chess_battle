using Godot;
using System;

using GameLogic;

public partial class NavigationAgent3dMoveTo : NavigationAgent3D
{
    [Export]
    UnitGameShow unitShowRef;


    private float _movementDelta;


    Vector3? targetPosition = null;

    public override void _Ready()
    {
        VelocityComputed += OnVelocityComputed;
    }


    public override void _PhysicsProcess(double delta)
    {
        if (targetPosition == null)
        {
            return;
        }

        TargetPosition = (Vector3)targetPosition;

        // Do not query when the map has never synchronized and is empty.
        if (NavigationServer3D.MapGetIterationId(GetNavigationMap()) == 0)
        {
            return;
        }

        if (IsNavigationFinished())
        {
            return;
        }

        unitShowRef.GlobalPosition = unitShowRef.UnitStateRec.Position;

        _movementDelta = unitShowRef.UnitStateRec.MoveSpeed * (float)delta;
        Vector3 nextPathPosition = GetNextPathPosition();
        Vector3 newVelocity = unitShowRef.GlobalPosition.DirectionTo(nextPathPosition) * _movementDelta;
        if (AvoidanceEnabled)
        {
            Velocity = newVelocity;
        }
        else
        {
            OnVelocityComputed(newVelocity);
        }
    }
    private void OnVelocityComputed(Vector3 safeVelocity)
    {
        Vector3 pos = unitShowRef.GlobalPosition;
        var newPos = pos.MoveToward(pos + safeVelocity, _movementDelta);
        unitShowRef.UnitStateRec.SetGlobalPosition(newPos);
    }
}
