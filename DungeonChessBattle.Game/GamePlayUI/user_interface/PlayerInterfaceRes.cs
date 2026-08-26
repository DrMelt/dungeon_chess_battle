using DungeonChessBattle.Game.GameAssets;
using Godot;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 战斗交互状态容器，记录鼠标悬停控件/单位、鼠标地面位置与目标等待状态。
/// 仅存值、不发信号：由输入采集（BattleInputController）写入，View 组件每帧直读做脏检查。
/// 服务端聚焦目标由 BattleSessionContext.LocalFocus 派生，不在此容器。
/// </summary>
[GlobalClass]
public partial class PlayerInterfaceRes : Resource {

    /// <summary>当前鼠标悬停的 UI 控件。</summary>
    public Control? MouseOnUIControl {
        get; set;
    }

    /// <summary>当前鼠标悬停的单位。</summary>
    public UnitGameShow? MouseOnUnit {
        get;
        set;
    }

    /// <summary>鼠标在地面的世界坐标位置。</summary>
    public Vector3? MouseGroundPosition {
        get;
        set;
    } = null;

    /// <summary>当前是否在等待技能目标选择（由 SkillsList 写入，战斗输入应阻塞）。</summary>
    public bool IsWaitingSkillTarget {
        get; set;
    }

}
