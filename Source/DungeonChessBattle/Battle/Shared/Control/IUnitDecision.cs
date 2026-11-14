using DungeonChessBattle.Battle.Shared.Camp;
using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Shared.Control;

/// <summary>
/// 自治决策算法：为单个单位产出当帧意图，自治意图源每逻辑帧调用一次。
/// 实现必须无状态，无状态实例可被任意多个单位共享。
/// 决策只依赖 <see cref="IBattleUnitView"/> 只读接口与调用方按副本注入的阵营关系，不接触网络载体，可脱离服务端独立测试。
/// </summary>
public interface IUnitDecision {
    /// <summary>
    /// 生成当帧意图。
    /// </summary>
    /// <param name="self">决策主体，仇恨取自其自身仇恨投影。</param>
    /// <param name="scene">战场查询视图，本帧读只读，禁止写。</param>
    /// <param name="relations">所在副本的阵营关系函数，敌我判定唯一来源。</param>
    UnitIntent Decide(IBattleUnitView self, IBattleSceneView scene, CampRelationResolver relations);
}
