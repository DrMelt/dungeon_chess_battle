using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 玩家界面交互状态资源，记录鼠标悬停控件/单位、焦点单位与鼠标地面位置，并对外发出信号。
/// </summary>
[GlobalClass]
public partial class PlayerInterfaceRes : Resource {

    /// <summary>鼠标悬停的 UI 控件变化信号。</summary>
    [Signal]
    public delegate void MouseOnUIControlChangedEventHandler(Control control);

    /// <summary>焦点单位变化信号。</summary>
    [Signal]
    public delegate void FocusOnUnitChangedEventHandler(UnitGameShow unit);

    /// <summary>当前鼠标悬停的 UI 控件，变化时发出信号。</summary>
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

    /// <summary>当前鼠标悬停的单位。</summary>
    public UnitGameShow? MouseOnUnit {
        get;
        set {
            if (field != value) {
                field = value;
            }
        }
    }

    /// <summary>当前焦点单位，变化时发出信号。</summary>
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

    /// <summary>鼠标在地面的世界坐标位置。</summary>
    public Vector3? MouseGoundPosition {
        get;
        set;
    } = null;

}
