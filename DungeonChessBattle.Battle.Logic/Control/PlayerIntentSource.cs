using System.Numerics;
using DungeonChessBattle.Battle.Logic.Combat;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Control;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Logic.Control;

/// <summary>
/// 玩家意图源：把一个玩家的输入产出为它所属单位的当帧意图。
/// 移动是状态——输入槽新包覆盖、读取不消耗，无新包的逻辑帧沿用槽内上一值，与网络侧末值保持同义；零向量是显式停止。
/// 施法是事件——提交即占该单位唯一待决槽并满窗计时，就绪即转投为当帧施法意图，超窗或施法者死亡即弃。
/// 提交与刷新同帧，故首次刷新只判就绪、窗口自下一帧起递减，与逐帧推进的窗口口径一致。
/// 死亡不拦移动：死亡判据在位移解算，读条打断口径与施法者存活无关。
/// </summary>
/// <param name="unitId">本源驱动的单位。</param>
/// <param name="scene">战斗世界，读施法者状态与冷却。</param>
/// <param name="logger">待决与作废日志，可选注入。</param>
internal sealed partial class PlayerIntentSource(
    UnitId unitId, BattleScene scene, ILogger<PlayerIntentSource>? logger = null) : IUnitIntentSource {
    /// <summary>预输入有效窗口秒数。服务端与回放必须同值，故不开放注入；改值属结算逻辑变更，须递增修订号。</summary>
    private const float PendingWindowSeconds = 0.5f;

    private readonly UnitId _unitId = unitId;
    private readonly BattleScene _scene = scene;

    /// <summary>待决与作废日志，未注入时用 NullLogger 静默。</summary>
    private readonly ILogger<PlayerIntentSource> _logger = logger ?? NullLogger<PlayerIntentSource>.Instance;

    /// <summary>移动输入槽。</summary>
    private Vector2 _move;

    /// <summary>待决施法意图，null 表示无待决；同一单位只保一条，新提交覆盖。</summary>
    private UnitCastIntent? _pendingCast;

    /// <summary>待决施法剩余窗口秒数。</summary>
    private float _pendingRemaining;

    /// <summary>待决施法是否本帧新提交：新提交只判就绪，不递减窗口。</summary>
    private bool _pendingFresh;

    /// <inheritdoc />
    public UnitIntent Intent {
        get; private set;
    }

    /// <summary>写入移动输入：覆盖输入槽，投递交刷新点。</summary>
    /// <param name="moveDirection">移动方向，零向量即停止。</param>
    public void SubmitMove(Vector2 moveDirection) => _move = moveDirection;

    /// <summary>提交施法意图：占待决槽并满窗计时；未就绪才记待决日志，转投在刷新点。</summary>
    /// <param name="cast">施法意图，目标与锚点按原样承载。</param>
    public void SubmitCast(in UnitCastIntent cast) {
        _pendingCast = cast;
        _pendingRemaining = PendingWindowSeconds;
        _pendingFresh = true;

        if (_scene.FindBattleUnit(_unitId) is { } caster && !SkillCastValidator.IsStateReady(caster, cast.Skill))
            LogCastQueued(caster.UnitName, cast.Skill.Id);
    }

    /// <inheritdoc />
    public void Refresh(float deltaTime) => Intent = new UnitIntent(_move, RefreshPendingCast(deltaTime));

    /// <summary>推进待决施法：施法者消失或已死亡即弃，非首次刷新递减窗口，就绪即转投为本帧施法意图。</summary>
    private UnitCastIntent? RefreshPendingCast(float deltaTime) {
        if (_pendingCast is not { } pending)
            return null;

        if (_scene.FindBattleUnit(_unitId) is not { IsDead: false } caster) {
            _pendingCast = null;
            return null;
        }

        if (_pendingFresh)
            _pendingFresh = false;
        else if ((_pendingRemaining -= deltaTime) <= 0f) {
            LogCastExpired(caster.UnitName, pending.Skill.Id);
            _pendingCast = null;
            return null;
        }

        if (!SkillCastValidator.IsStateReady(caster, pending.Skill))
            return null;

        _pendingCast = null;
        return pending;
    }

    #region 日志

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "[PlayerIntent] {Caster} cast queued for pre-input: {SkillId}.")]
    private partial void LogCastQueued(string caster, string skillId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "[PlayerIntent] {Caster} queued cast expired: {SkillId}.")]
    private partial void LogCastExpired(string caster, string skillId);

    #endregion
}
