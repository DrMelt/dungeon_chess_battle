using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Inputs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Logic;

/// <summary>
/// 权威输入门面：宿主提交玩家命令、推进在架意图的唯一入口，服务端与回放共用。
/// 键一律为网络 ID，载荷合法性只在此判一次，施法者与目标在门内解析为领域单位，解析不到即不接管。
/// 战斗推进不经本门面：<see cref="BattleScene.Tick"/>、单位增删与阶段写由宿主直接驱动 <see cref="BattleScene"/>。
/// </summary>
/// <param name="scene">战斗世界，意图的落地方。</param>
/// <param name="loggerFactory">排队、作废与拒绝日志工厂，可选注入；不注入则静默。</param>
public sealed partial class BattleIntentHub(BattleScene scene, ILoggerFactory? loggerFactory = null) {
    /// <summary>施法预输入缓冲，仅本门面可达，宿主无从直取。</summary>
    private readonly CastPreInputBuffer _preInput = new(scene, loggerFactory?.CreateLogger<CastPreInputBuffer>());

    /// <summary>命令拒绝日志，未注入时用 NullLogger 静默。</summary>
    private readonly ILogger<BattleIntentHub> _logger =
        loggerFactory?.CreateLogger<BattleIntentHub>() ?? NullLogger<BattleIntentHub>.Instance;

    /// <summary>
    /// 每个战斗 tick 在 <see cref="BattleScene.Tick"/> 之前调用一次：AI 决策 → 在架施法重试。
    /// 这个先后是注入侧唯一保留判定的顺序：同一单位后写覆盖先写，服务端与回放同序才复现同一结果。
    /// </summary>
    public void PrepareTick(float deltaTime) {
        scene.ApplyDecisions();
        _preInput.Advance(deltaTime);
    }

    /// <summary>
    /// 提交一条玩家命令，按类型落地：移动与施法是本帧意图，随 <see cref="BattleScene.Tick"/> 末作废，
    /// 由输入源逐 tick 重投；施法交排队器接管，未就绪入该单位排队槽；聚焦是持续状态，设定后保持。
    /// </summary>
    /// <returns>已接管返回 true，施法不含可施放性结论——裁定在 <see cref="BattleScene.Tick"/> 的读条推进段；
    /// false 只源于施法阶段非 Running、技能键非法、单位或目标解析不到/已死亡、<c>Kind</c> 非法。</returns>
    public bool Submit(in PlayerCommand cmd) => cmd.Kind switch {
        PlayerCommandKind.Move => scene.SubmitMove(cmd.NetId, cmd.MoveDir),
        PlayerCommandKind.Cast => SubmitCast(cmd),
        PlayerCommandKind.Focus => SubmitFocus(cmd),
        _ => false,
    };

    /// <summary>丢弃全部在架施法意图：其施法者引用在单位重建后失效，宿主重置前必须调用。</summary>
    public void ClearQueuedCasts() => _preInput.Clear();

    /// <summary>施法命令落地：技能键空值与超长校验、施法者与目标 ID 解析在此收口，位置锚点按目标类型取舍。</summary>
    private bool SubmitCast(in PlayerCommand cmd) {
        if (scene.CurrentPhase != BattlePhase.Running) {
            LogRejected(cmd, "battle not running");
            return false;
        }

        if (cmd.SkillKey is not { Length: > 0 } skillKey || skillKey.Length > SkillKeyId.MaxKeyLength) {
            LogRejected(cmd, "skill key invalid or too long");
            return false;
        }

        if (scene.FindBattleUnit(cmd.NetId) is not { } caster) {
            LogRejected(cmd, "caster not found");
            return false;
        }

        BattleUnit? target = null;
        if (cmd.TargetNetId != 0) {
            if (scene.FindBattleUnit(cmd.TargetNetId) is not { } targetUnit) {
                LogRejected(cmd, "target not found");
                return false;
            }
            target = targetUnit;
        }

        _preInput.Submit(caster, new CastIntent(new SkillKeyId(skillKey), target, cmd.CastTargetPos));
        return true;
    }

    /// <summary>聚焦命令落地：存活校验交战斗世界，与 <c>Tick</c> 内的清活同源。</summary>
    private bool SubmitFocus(in PlayerCommand cmd) {
        if (scene.SubmitFocus(cmd.NetId, cmd.TargetNetId))
            return true;
        LogRejected(cmd, "focus unit missing or target dead");
        return false;
    }

    #region 日志

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "[IntentHub] Command rejected ({Reason}): {Command}.")]
    private partial void LogRejected(PlayerCommand command, string reason);

    #endregion
}

