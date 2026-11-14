using DungeonChessBattle.Battle.Shared;
using DungeonChessBattle.Battle.Shared.Camp;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Control;

namespace DungeonChessBattle.Battle.Logic.Control;

/// <summary>
/// 自治意图源：每逻辑帧调用一次决策算法，把产出当作当帧意图。
/// 死亡与读条中不决策——读条中投移动会打断自身读条。
/// 决策算法可被多单位共享，共享时各单位仍各持一份本源，本帧产出互不干扰。
/// </summary>
/// <param name="unitId">本源驱动的单位。</param>
/// <param name="decision">决策算法。</param>
/// <param name="scene">战场只读视图，决策只读消费。</param>
/// <param name="relations">副本阵营关系函数，敌我判定唯一来源。</param>
internal sealed class AiIntentSource(
    UnitId unitId, IUnitDecision decision, IBattleSceneView scene, CampRelationResolver relations) : IUnitIntentSource {
    /// <inheritdoc />
    public UnitIntent Intent {
        get; private set;
    }

    /// <inheritdoc />
    public void Refresh(float deltaTime) {
        if (scene.FindUnit(unitId) is not { IsDead: false } self || self.SkillCasting != default) {
            Intent = UnitIntent.Idle;
            return;
        }

        Intent = decision.Decide(self, scene, relations);
    }
}
