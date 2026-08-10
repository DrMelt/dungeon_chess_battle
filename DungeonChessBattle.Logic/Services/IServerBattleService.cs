using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// Logic 层单房间门面服务接口。每个战斗房间持有独立实现（<see cref="GameLogicService"/>），
/// 直接承载本房间的 GameRoom、战斗流程与单位状态，方法不再携带房间 ID。
/// 战斗流程面向 <see cref="IBattle"/> 抽象接口，不暴露内部实现类型；
/// 技能结算与 Buff 推进以单位集合为上下文，不携带战斗管理器参数。
/// </summary>
public interface IServerBattleService {
    /// <summary>本房间数据（Logic 权威持有战斗字段所有权）。</summary>
    GameRoom Room {
        get;
    }

    /// <summary>本房间的战斗单位状态列表（Logic 权威）。</summary>
    List<UnitModel> GetUnits();

    /// <summary>在本房间创建单位。</summary>
    IUnitState CreateUnit(string unitName, string camp);

    /// <summary>开始本房间战斗。</summary>
    IBattle StartBattle();

    /// <summary>获取本房间的战斗流程；不存在时返回 null。</summary>
    IBattle? GetBattle();

    /// <summary>按帧推进本房间的战斗逻辑。</summary>
    void TickBattle(float deltaTime);

    /// <summary>结束本房间指定战斗。</summary>
    void EndBattle(IBattle battle);

    /// <summary>注入技能解析器（服务端持有配置表）。</summary>
    void SetSkillResolver(Func<ushort, SkillModel?> resolver);

    /// <summary>发起读条施法（冷却校验通过返回 true）。</summary>
    bool BeginSpell(string casterName, ushort skillId, string? targetName,
        System.Numerics.Vector3? targetPos = null);

    /// <summary>按帧推进读条，返回本帧完成读条并结算的单位名称集合。</summary>
    IReadOnlyList<string> TickSpells(float deltaTime);

    /// <summary>按帧推进本房间所有单位的冷却。</summary>
    void TickCooldowns(float deltaTime);

    /// <summary>查询单位指定技能剩余个体冷却秒数。</summary>
    float GetSkillCooldownRemaining(string unitName, ushort skillId);

    /// <summary>对目标施放技能（服务端权威结算）。</summary>
    void CastSkill(IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null);

    /// <summary>按帧推进单位集合的 Buff 状态。</summary>
    void UpdateBuffs(IEnumerable<IUnitState> units, double deltaTime);

    /// <summary>判断本房间战斗是否已结束。</summary>
    bool CheckBattleEnded();

    /// <summary>释放本房间战斗上下文（房间销毁时调用）。</summary>
    void Dispose();
}
