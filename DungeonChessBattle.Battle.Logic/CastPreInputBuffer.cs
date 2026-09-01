using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Logic.Combat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Logic;

/// <summary>
/// 施法预输入缓冲：把玩家一次按键推迟到状态就绪的 tick 再交回 <see cref="BattleScene"/> 裁定。
/// 只持技能键、目标引用与剩余有效秒数，不含射程、阵营等任何领域判定：重试判据唯一为
/// <see cref="SkillCastValidator.IsStateReady"/>——会自然转就绪的状态阻塞才值得等待，
/// 目标条件一律在提交时由战斗世界一次裁定，被拒即弃。
/// 排队状态是宿主待办，不写进领域单位、不进同步通道。
/// 由权威宿主在 <see cref="BattleScene.Tick"/> 之前的输入窗口驱动，服务端与回放同序同实现，落地逐位复现。
/// </summary>
/// <param name="scene">意图落地时提交到的战斗世界。</param>
/// <param name="logger">可选日志注入。</param>
public sealed partial class CastPreInputBuffer(BattleScene scene, ILogger<CastPreInputBuffer>? logger = null) {
    /// <summary>
    /// 预输入有效窗口秒数。域内常量不开放注入：服务端与回放必须同值，
    /// 分档调窗属战斗内容变更，须与既有录像的兼容决策一并处理。
    /// </summary>
    public const float WindowSeconds = 0.5f;

    /// <summary>意图落地时提交到的战斗世界，构造后只读，与之同生命周期。</summary>
    private readonly BattleScene _scene = scene;

    /// <summary>排队与作废日志，未注入时用 NullLogger 静默。</summary>
    private readonly ILogger<CastPreInputBuffer> _logger = logger ?? NullLogger<CastPreInputBuffer>.Instance;

    /// <summary>在架意图，按提交顺序扫描，服务端与回放同序；同一施法者只保一条，新意图覆盖旧意图。</summary>
    private readonly List<PendingIntent> _intents = [];

    /// <summary>本帧需移除意图的暂存，避免前向扫描时就地删改集合。</summary>
    private readonly List<PendingIntent> _retired = [];

    /// <summary>
    /// 提交一次施法意图：状态已就绪立即交战斗世界裁定，返回值即裁定结果；
    /// 未就绪则覆盖该施法者的在架意图并满窗计时，返回 true 仅表示已被接管，不保证最终可施放。
    /// </summary>
    public bool Submit(BattleUnit caster, SkillKeyId skillKey, BattleUnit? target, Vector2? targetPos) {
        if (SkillCastValidator.IsStateReady(caster, skillKey))
            return _scene.TryCast(caster, skillKey, target, targetPos);

        _intents.RemoveAll(i => ReferenceEquals(i.Caster, caster));
        _intents.Add(new PendingIntent(caster, skillKey, target, targetPos, WindowSeconds));
        LogCastQueued(caster.UnitName, skillKey.Id);
        return true;
    }

    /// <summary>
    /// 逐帧推进在架意图：剩余秒随本帧递减，超窗或施法者死亡即弃；状态就绪则提交一次并清槽，
    /// 被战斗世界拒绝同样清槽。阶段非 Running 不推进，与战斗世界时钟同起停。
    /// </summary>
    public void Advance(float deltaTime) {
        if (_scene.CurrentPhase != BattlePhase.Running || _intents.Count == 0)
            return;

        // 前向扫描保两端提交顺序一致；本帧已落地的意图只改他人生命与自身冷却，不改后续意图的判据
        foreach (var intent in _intents) {
            if (intent.Caster.IsDead) {
                _retired.Add(intent);
                continue;
            }

            intent.Remaining -= deltaTime;
            if (intent.Remaining <= 0f) {
                LogCastExpired(intent.Caster.UnitName, intent.SkillKey.Id);
                _retired.Add(intent);
                continue;
            }

            if (!SkillCastValidator.IsStateReady(intent.Caster, intent.SkillKey))
                continue;

            _scene.TryCast(intent.Caster, intent.SkillKey, intent.Target, intent.TargetPos);
            _retired.Add(intent);
        }

        foreach (var intent in _retired)
            _intents.Remove(intent);
        _retired.Clear();
    }

    /// <summary>清空全部在架意图：回放重置重建单位后必须调用，在架的旧单位引用随重建失效。</summary>
    public void Clear() {
        _intents.Clear();
        _retired.Clear();
    }

    /// <summary>
    /// 在架施法意图：目标持领域单位引用，与读条目标 <c>UnitCombatState.CastTarget</c> 同源，不做 ID 重解析。
    /// </summary>
    private sealed class PendingIntent(BattleUnit caster, SkillKeyId skillKey, BattleUnit? target,
        Vector2? targetPos, float remaining) {
        public readonly BattleUnit Caster = caster;
        public readonly SkillKeyId SkillKey = skillKey;
        public readonly BattleUnit? Target = target;
        public readonly Vector2? TargetPos = targetPos;
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
