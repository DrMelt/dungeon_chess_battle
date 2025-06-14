using Godot;
using System;

public partial class UserOperationInterfaceInfo : Node
{
    [Export]
    Camera3D camera3DRef;
    [Export]
    Node2d_UserUI node2dUiRef;


    [ExportGroup("Intrinsic Parameter")]

    [Export]
    RayCast3D rayCast3DRef;
    [Export]
    MeshInstance3D outLineMeshInstance;

    UnitGameShow _mouseOnUnit;
    public UnitGameShow MouseOnUnit
    {
        get => _mouseOnUnit;
        set
        {
            if (_mouseOnUnit != value)
            {
                _mouseOnUnit = value;
                OnMouseOnUnitChanged();
            }
        }
    }

    UnitGameShow _focusOnUnit;
    public UnitGameShow FocusOnUnit
    {
        get => _focusOnUnit;
        set
        {
            if (_focusOnUnit != value)
            {
                if (_focusOnUnit != null)
                {
                    _focusOnUnit.TargetDecalRef.SetMark_Normal();
                }

                _focusOnUnit = value;

                if (_focusOnUnit != null)
                {
                    _focusOnUnit.TargetDecalRef.SetMark_Focus(_focusOnUnit);
                }

                FocusOnUnitChangedEvent.Invoke(_focusOnUnit);
            }
        }
    }


    Vector3? _mouseGoundPosition = null;
    public Vector3? MouseGoundPosition
    {
        get => _mouseGoundPosition;
        private set
        {
            _mouseGoundPosition = value;
        }
    }


    public Action<UnitGameShow> FocusOnUnitChangedEvent;

    void OnMouseOnUnitChanged()
    {
        if (MouseOnUnit != null)
        {
            outLineMeshInstance.GlobalPosition = MouseOnUnit.UnitMeshInstanceRef.GlobalPosition;
            outLineMeshInstance.Mesh = MouseOnUnit.UnitMeshInstanceRef.Mesh;
        }
        else
        {
            outLineMeshInstance.Mesh = null;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 mousePos = GetViewport().GetMousePosition();
        Vector3 from = camera3DRef.ProjectRayOrigin(mousePos);
        rayCast3DRef.Position = from;
        rayCast3DRef.TargetPosition = camera3DRef.ProjectRayNormal(mousePos) * 1000.0f;

        bool isSetted = false;
        if (rayCast3DRef.IsColliding())
        {
            if (rayCast3DRef.GetCollider() is UnitShowArea3D area3D)
            {
                MouseOnUnit = area3D.UnitShowRef;
                isSetted = true;
            }
        }

        if (!isSetted)
        {
            MouseOnUnit = null;
        }

        if (rayCast3DRef.GetCollider() is StaticBody3D staticBody3D)
        {
            if ((staticBody3D.CollisionLayer & 2 ^ 5) > 0)
            {
                MouseGoundPosition = rayCast3DRef.GetCollisionPoint();
            }
            else
            {
                MouseGoundPosition = null;
            }
        }
    }


    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("Interface_SelectFocus"))
        {
            if (!node2dUiRef.IsWaitSkillTarget() && !node2dUiRef.IsMouseOn)
            {
                FocusOnUnit = MouseOnUnit;
            }
        }
    }

}
