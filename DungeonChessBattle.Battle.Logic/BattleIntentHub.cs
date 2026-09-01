using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Logic;

/// <summary>
/// 权威输入门面：宿主提交移动与施法意图、推进在架意图的唯一入口，服务端与回放共用。
/// 键一律为网络 ID，施法者与目标在门内解析为领域单位，解析不到即不接管。
/// 战斗推进不经本门面：<see cref="BattleScene.Tick"/>、单位增删与阶段写由宿主直接驱动 <see cref="BattleScene"/>。
/// </summary>
/// <param name="scene">战斗世界，意图的落地方。</param>
/// <param name="loggerFactory">排队与作废日志工厂，可选注入；不注入则静默。</param>
public sealed class BattleIntentHub(BattleScene scene, ILoggerFactory? loggerFactory = null) {
    /// <summary>施法预输入缓冲，仅本门面可达，宿主无从直取。</summary>
    private readonly CastPreInputBuffer _preInput = new(scene, loggerFactory?.CreateLogger<CastPreInputBuffer>());

    /// <summary>
    /// 每个战斗 tick 在 <see cref="BattleScene.Tick"/> 之前调用一次：AI 决策 → 在架施法重试。
    /// 这个先后是注入侧唯一保留判定的顺序：同一单位后写覆盖先写，服务端与回放同序才复现同一结果。
    /// </summary>
    public void PrepareTick(float deltaTime) {
        scene.ApplyDecisions();
        _preInput.Advance(deltaTime);
    }

    /// <summary>提交移动意图：写入该单位本帧移动输入，单位不存在即丢弃；非零位移在读条推进段打断当前读条。</summary>
    public void SubmitMove(ushort netId, Vector2 moveDirection) => scene.SubmitMove(netId, moveDirection);

    /// <summary>
    /// 提交施法意图，交排队器接管：状态就绪即转投为该单位的本帧意图，未就绪入其排队槽。
    /// targetNetId 非 0 走单位目标、为 0 走位置目标。
    /// </summary>
    /// <returns>已投递接管返回 true，不含可施放性结论——裁定在 <see cref="BattleScene.Tick"/> 的读条推进段完成；
    /// false 只源于施法者或目标解析不到，此时不投递。</returns>
    public bool SubmitCast(ushort casterNetId, SkillKeyId skillKey, ushort targetNetId, Vector2? targetPos) {
        if (scene.FindBattleUnit(casterNetId) is not { } caster)
            return false;

        BattleUnit? target = null;
        if (targetNetId != 0) {
            if (scene.FindBattleUnit(targetNetId) is not { } targetUnit)
                return false;
            target = targetUnit;
        }

        _preInput.Submit(caster, new CastIntent(skillKey, target, targetPos));
        return true;
    }

    /// <summary>丢弃全部在架施法意图：其施法者引用在单位重建后失效，宿主重置前必须调用。</summary>
    public void ClearQueuedCasts() => _preInput.Clear();
}
