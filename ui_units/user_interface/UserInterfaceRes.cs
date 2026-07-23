using Godot;
using System;

namespace DungeonChessBattle;

[GlobalClass]
public partial class UserInterfaceRes : Resource {

    Control _mouseOnUIControl;
    public Control MouseOnUIControl {
        get => _mouseOnUIControl;
        set {
            if (_mouseOnUIControl != value) {
                _mouseOnUIControl = value;
                MouseOnUIControlChangedEvent?.Invoke(_mouseOnUIControl);
            }
        }
    }
    public event Action<Control> MouseOnUIControlChangedEvent;


    UnitGameShow _mouseOnUnit;
    public UnitGameShow MouseOnUnit {
        get => _mouseOnUnit;
        set {
            if (_mouseOnUnit != value) {
                _mouseOnUnit = value;
            }
        }
    }

    UnitGameShow _focusOnUnit;
    public UnitGameShow FocusOnUnit {
        get => _focusOnUnit;
        set {
            if (_focusOnUnit != value) {
                _focusOnUnit = value;
                FocusOnUnitChangedEvent.Invoke(_focusOnUnit);
            }
        }
    }
    public event Action<UnitGameShow> FocusOnUnitChangedEvent;

    Vector3? _mouseGoundPosition = null;
    public Vector3? MouseGoundPosition {
        get => _mouseGoundPosition;
        set {
            _mouseGoundPosition = value;
        }
    }

}
