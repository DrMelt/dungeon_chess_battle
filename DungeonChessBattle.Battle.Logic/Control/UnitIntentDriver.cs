using System.Numerics;
using DungeonChessBattle.Battle.Config.Shared.Control;
using DungeonChessBattle.Battle.Shared.Camp;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Control;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Logic.Control;

/// <summary>
/// 意图驱动：按单位登记意图源，每逻辑帧按世界注册顺序刷新全部意图源，并把产出经战斗世界的意图写入面投递。
/// 玩家命令在此转写为该单位玩家意图源的输入，意图源实现不对宿主暴露。
/// 刷新时机由 <see cref="BattleIntentHub.PrepareTick"/> 单点保证，服务端与回放同序才复现同一结果。
/// 不含决策算法与输入采集；未登记意图源的单位不产出意图。
/// </summary>
/// <param name="scene">战斗世界，世界顺序来源与意图写入面。</param>
/// <param name="relations">副本阵营关系函数，注入自治意图源。</param>
/// <param name="loggerFactory">玩家意图源日志工厂，可选注入。</param>
public sealed class UnitIntentDriver(
    BattleScene scene, CampRelationResolver relations, ILoggerFactory? loggerFactory = null) {
    /// <summary>单位 ID 到意图源，同一单位重复登记覆盖。</summary>
    private readonly Dictionary<UnitId, IUnitIntentSource> _sources = [];

    /// <summary>登记玩家输入轨道意图源：驱动方式由宿主按单位进入战场的方式决定，不经配置推断。</summary>
    public void RegisterPlayer(UnitId unitId) =>
        _sources[unitId] = new PlayerIntentSource(unitId, scene, loggerFactory?.CreateLogger<PlayerIntentSource>());

    /// <summary>登记自治意图源：按单位配置声明的控制者建源。</summary>
    public void RegisterAutonomous(UnitId unitId, UnitControllerConfig controller) =>
        _sources[unitId] = new AiIntentSource(unitId, controller.Decision, scene, relations);

    /// <summary>把玩家移动输入转写到该单位玩家意图源的输入槽。</summary>
    /// <returns>单位由玩家控制并已写入返回 true。</returns>
    public bool SubmitMove(UnitId unitId, Vector2 moveDirection) {
        if (_sources.TryGetValue(unitId, out var source) && source is PlayerIntentSource player) {
            player.SubmitMove(moveDirection);
            return true;
        }
        return false;
    }

    /// <summary>把玩家施法意图转写到该单位玩家意图源的待决槽。</summary>
    /// <returns>单位由玩家控制并已写入返回 true。</returns>
    public bool SubmitCast(UnitId unitId, in UnitCastIntent cast) {
        if (_sources.TryGetValue(unitId, out var source) && source is PlayerIntentSource player) {
            player.SubmitCast(cast);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 推进本帧意图：逐单位刷新意图源并投递其移动与施法意图，只在 Running 阶段推进。
    /// 遍历按世界注册顺序，两端同序；投递的意图由 <see cref="BattleScene.Tick"/> 末统一作废。
    /// </summary>
    /// <param name="deltaTime">距上一逻辑帧的间隔秒数。</param>
    public void Advance(float deltaTime) {
        if (scene.CurrentPhase != BattlePhase.Running)
            return;

        foreach (var view in scene.Units)
            RefreshAndApply(view.UnitId, deltaTime);
    }

    /// <summary>刷新一个单位的意图源并投递其当帧意图：未登记意图源即不推进，移动总投，无施法意图即不投施法。</summary>
    private void RefreshAndApply(UnitId unitId, float deltaTime) {
        if (!_sources.TryGetValue(unitId, out var source))
            return;

        source.Refresh(deltaTime);
        var intent = source.Intent;
        scene.SubmitMove(unitId, intent.Move);
        if (intent.Cast is { } cast)
            scene.SubmitCastIntent(unitId, cast);
    }
}
