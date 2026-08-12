using DungeonChessBattle.GameAssets;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 战斗交互共享状态，记录鼠标悬停控件/单位、焦点单位、鼠标地面位置与目标等待状态并发出信号。
/// 作为战斗 UI 层唯一跨组件状态总线：输入采集（BattleInputController）写入悬停与地面点，
/// 服务端聚焦由 BattleUnitManager 桥接写入，View 组件订阅事件刷新显示。
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
        set;
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
    public Vector3? MouseGroundPosition {
        get;
        set;
    } = null;

    /// <summary>当前是否在等待技能目标选择（由 SkillsList 写入）。</summary>
    public bool IsWaitingSkillTarget {
        get; set;
    }

    /// <summary>当前是否在等待移动目标选择。</summary>
    public bool IsWaitingMoveTarget {
        get; set;
    }

    /// <summary>当前是否在等待任何目标选择（战斗输入应阻塞）。</summary>
    public bool IsWaitingTarget => IsWaitingSkillTarget || IsWaitingMoveTarget;

}
