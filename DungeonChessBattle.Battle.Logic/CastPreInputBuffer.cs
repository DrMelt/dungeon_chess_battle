using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Logic.Combat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Logic;

/// <summary>
/// 施法预输入缓冲：把状态未就绪的按键推迟到就绪的 tick，再转投为该单位的 <c>BattleUnit.CastInput</c>。
/// 唯一重试判据是 <see cref="SkillCastValidator.IsStateReady"/>；射程、阵营等目标条件一律不预判，就绪时转投、被拒即弃。
/// 由 <see cref="BattleIntentHub"/> 私有持有，在 <see cref="BattleScene.Tick"/> 之前推进，服务端与回放同序。
/// </summary>
/// <param name="scene">战斗世界，只读其阶段以与战斗时钟同起停。</param>
/// <param name="logger">可选日志注入。</param>
internal sealed partial class CastPreInputBuffer(BattleScene scene, ILogger<CastPreInputBuffer>? logger = null) {
    /// <summary>
    /// 预输入有效窗口秒数。服务端与回放必须同值，故不开放注入；改值属战斗内容变更，须一并决定既有录像去留。
    /// </summary>
    public const float WindowSeconds = 0.5f;

    /// <summary>战斗世界，仅 <see cref="BattleScene.CurrentPhase"/> 一个读者，与之同生命周期。</summary>
    private readonly BattleScene _scene = scene;

    /// <summary>排队与作废日志，未注入时用 NullLogger 静默。</summary>
    private readonly ILogger<CastPreInputBuffer> _logger = logger ?? NullLogger<CastPreInputBuffer>.Instance;

    /// <summary>在架意图，按提交顺序扫描，服务端与回放同序；同一施法者只保一条，新意图覆盖旧意图。</summary>
    private readonly List<PendingIntent> _intents = [];

    /// <summary>本帧需移除意图的暂存，避免前向扫描时就地删改集合。</summary>
    private readonly List<PendingIntent> _retired = [];

    /// <summary>
    /// 提交一次施法意图：状态就绪即转投为该单位的本帧意图，未就绪则覆盖其排队槽并满窗计时。
    /// 两条分支都是接管，裁定推迟到消费点。
    /// </summary>
    public void Submit(BattleUnit caster, CastIntent intent) {
        if (SkillCastValidator.IsStateReady(caster, intent.Skill)) {
            caster.CastInput = intent;
            return;
        }

        _intents.RemoveAll(i => ReferenceEquals(i.Caster, caster));
        _intents.Add(new PendingIntent(caster, intent, WindowSeconds));
        LogCastQueued(caster.UnitName, intent.Skill.Id);
    }

    /// <summary>
    /// 逐帧推进在架意图：剩余秒随本帧递减，超窗或施法者死亡即弃；状态就绪则转投为本帧意图并清槽。
    /// 转投后本类不再介入，阶段非 Running 不推进。
    /// </summary>
    public void Advance(float deltaTime) {
        if (_scene.CurrentPhase != BattlePhase.Running || _intents.Count == 0)
            return;

        // 前向扫描保持两端提交顺序一致；本帧转投只写意图字段，不影响后续意图的就绪判据
        foreach (var intent in _intents) {
            if (intent.Caster.IsDead) {
                _retired.Add(intent);
                continue;
            }

            intent.Remaining -= deltaTime;
            if (intent.Remaining <= 0f) {
                LogCastExpired(intent.Caster.UnitName, intent.Intent.Skill.Id);
                _retired.Add(intent);
                continue;
            }

            if (!SkillCastValidator.IsStateReady(intent.Caster, intent.Intent.Skill))
                continue;

            intent.Caster.CastInput = intent.Intent;
            _retired.Add(intent);
        }

        foreach (var intent in _retired)
            _intents.Remove(intent);
        _retired.Clear();
    }

    /// <summary>清空全部在架意图：其施法者引用在单位重建后失效，宿主重置前必须调用。</summary>
    public void Clear() {
        _intents.Clear();
        _retired.Clear();
    }

    /// <summary>在架施法意图：载荷与转投目标 <c>BattleUnit.CastInput</c> 同形态，就绪即整体转投。</summary>
    private sealed class PendingIntent(BattleUnit caster, CastIntent intent, float remaining) {
        public readonly BattleUnit Caster = caster;
        public readonly CastIntent Intent = intent;
        public float Remaining = remaining;
    }

    #region 日志

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "[CastPreInput] {Caster} cast queued for pre-input: {SkillId}.")]
    private partial void LogCastQueued(string caster, string skillId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "[CastPreInput] {Caster} queued cast expired: {SkillId}.")]
    private partial void LogCastExpired(string caster, string skillId);

    #endregion
}
