using DungeonChessBattle.Battle.Logic.Control;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Control;
using DungeonChessBattle.Battle.Shared.Inputs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Logic;

/// <summary>
/// 权威输入门面：玩家命令提交的唯一入口与本帧意图准备的唯一推进点，服务端与回放共用。
/// 键一律为 <see cref="UnitId"/>，载荷合法性只在此判一次，施法者与目标在门内解析，解析不到即不接管。
/// 命令按类型分流：移动与施法写该单位的玩家意图源，聚焦是持续展示态，直达战斗世界。
/// 战斗推进不经本门面：<see cref="BattleScene.Tick"/>、单位增删与阶段写由宿主直接驱动 <see cref="BattleScene"/>。
/// </summary>
/// <param name="scene">战斗世界，聚焦态的落地方。</param>
/// <param name="driver">意图驱动：本门面把玩家命令写入它登记的意图源，并按帧推进它。</param>
/// <param name="loggerFactory">命令拒绝日志工厂，可选注入；不注入则静默。</param>
public sealed partial class BattleIntentHub(BattleScene scene, UnitIntentDriver driver, ILoggerFactory? loggerFactory = null) {
    /// <summary>命令拒绝日志，未注入时用 NullLogger 静默。</summary>
    private readonly ILogger<BattleIntentHub> _logger =
        loggerFactory?.CreateLogger<BattleIntentHub>() ?? NullLogger<BattleIntentHub>.Instance;

    /// <summary>
    /// 每个战斗 tick 在 <see cref="BattleScene.Tick"/> 之前调用一次：刷新全部意图源并投递其当帧意图，玩家与自治同路。
    /// 这个先后是注入侧唯一保留判定的顺序：同一单位后写覆盖先写，服务端与回放同序才复现同一结果。
    /// </summary>
    /// <param name="deltaTime">距上一逻辑帧的间隔秒数，供待决施法递减预输入窗口。</param>
    public void PrepareTick(float deltaTime) => driver.Advance(deltaTime);

    /// <summary>
    /// 提交一条玩家命令，按类型落地：移动写该单位意图源的输入槽、施法写其待决槽，二者随 <see cref="PrepareTick"/>
    /// 产出为本帧意图并在 <see cref="BattleScene.Tick"/> 末作废，由意图源逐帧重新产出；聚焦是持续状态，设定后保持。
    /// </summary>
    /// <returns>已接管返回 true，施法不含可施放性结论——裁定在 <see cref="BattleScene.Tick"/> 的读条推进段；
    /// false 只源于施法阶段非 Running、技能键非法、单位或目标解析不到、单位不由玩家控制、<c>Kind</c> 非法。
    /// 死亡不经此处拒：待决施法在刷新点按施法者存活与状态丢弃。</returns>
    public bool Submit(in PlayerCommand cmd) => cmd.Kind switch {
        PlayerCommandKind.Move => SubmitMove(cmd),
        PlayerCommandKind.Cast => SubmitCast(cmd),
        PlayerCommandKind.Focus => SubmitFocus(cmd),
        _ => false,
    };

    /// <summary>聚焦命令落地：存活校验交战斗世界，与 <c>Tick</c> 内的清活同源。</summary>
    private bool SubmitFocus(in PlayerCommand cmd) {
        if (scene.SubmitFocus(cmd.SourceUnitId, cmd.TargetUnitId))
            return true;
        LogRejected(cmd, "focus unit missing or target dead");
        return false;
    }

    /// <summary>移动命令落地：转写进该单位玩家意图源的输入槽，产出点在 <see cref="PrepareTick"/>。</summary>
    private bool SubmitMove(in PlayerCommand cmd) {
        if (driver.SubmitMove(cmd.SourceUnitId, cmd.MoveDir))
            return true;
        LogRejected(cmd, "unit is not player controlled");
        return false;
    }

    /// <summary>
    /// 施法命令落地：技能键空值与超长校验、施法者与目标 ID 解析在此收口，位置锚点按目标类型取舍，
    /// 转写进该单位玩家意图源的待决槽，就绪与作废在刷新点按当时状态裁定。
    /// </summary>
    private bool SubmitCast(in PlayerCommand cmd) {
        if (scene.CurrentPhase != BattlePhase.Running) {
            LogRejected(cmd, "battle not running");
            return false;
        }

        if (cmd.SkillKey is not { Length: > 0 } skillKey || skillKey.Length > SkillKeyId.MaxKeyLength) {
            LogRejected(cmd, "skill key invalid or too long");
            return false;
        }

        if (scene.FindBattleUnit(cmd.SourceUnitId) is null) {
            LogRejected(cmd, "caster not found");
            return false;
        }

        if (!cmd.TargetUnitId.IsDefault && scene.FindBattleUnit(cmd.TargetUnitId) is null) {
            LogRejected(cmd, "target not found");
            return false;
        }

        if (!driver.SubmitCast(cmd.SourceUnitId, new UnitCastIntent(new SkillKeyId(skillKey), cmd.TargetUnitId, cmd.CastTargetPos))) {
            LogRejected(cmd, "caster is not player controlled");
            return false;
        }

        return true;
    }

    #region 日志

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "[IntentHub] Command rejected ({Reason}): {Command}.")]
    private partial void LogRejected(PlayerCommand command, string reason);

    #endregion
}


