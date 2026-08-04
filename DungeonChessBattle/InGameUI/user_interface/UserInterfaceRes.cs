using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class UserInterfaceRes : Resource {

    [Signal]
    public delegate void MouseOnUIControlChangedEventHandler(Control control);

    [Signal]
    public delegate void FocusOnUnitChangedEventHandler(UnitGameShow unit);

    public Control? MouseOnUIControl {
        get;
        set {
            if (field != value) {
                field = value;
                if (field != null)
                    EmitSignalMouseOnUIControlChanged(field);
            }
        }
    }

    public UnitGameShow? MouseOnUnit {
        get;
        set {
            if (field != value) {
                field = value;
            }
        }
    }

    public UnitGameShow? FocusOnUnit {
        get;
        set {
            if (field != value) {
                field = value;
                if (field != null)
                    EmitSignalFocusOnUnitChanged(field);
            }
        }
    }


    public Vector3? MouseGoundPosition {
        get;
        set;
    } = null;

}
