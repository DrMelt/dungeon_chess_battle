using DungeonChessBattle.Battle.Config.Shared.Buffs;
using DungeonChessBattle.Battle.Config.Shared.Combat;
using DungeonChessBattle.Battle.Config.Shared.Content;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Mod.Shared;

/// <summary>
/// mod 引导上下文：数据入口初始化时拿到的唯一句柄，提供内容定义注册与日志通道。
/// 内容按领域对象直接注册，定义对象是运行时强类型，非字符串 schema——mod 必先构造对象图再注册；
/// 行为实现随内容就地构造并注入定义，不经 ID 查表。
/// 同键后写覆盖；Buff 以 <see cref="BuffDefinition.BuffTypeId"/> 为同步身份。
/// 日志通道来自宿主：mod 在装配期取记录器，运行期按自身类别名记录诊断。
/// </summary>
public interface IModBootstrapContext {
    /// <summary>宿主注入的日志工厂，mod 据此按自身类别名建记录器。</summary>
    ILoggerFactory LoggerFactory {
        get;
    }

    /// <summary>注册技能定义，同 SkillId 覆盖。</summary>
    void RegisterSkill(SkillDefinition skill);

    /// <summary>注册 Buff 定义，同 BuffTypeId 覆盖。</summary>
    void RegisterBuff(BuffDefinition buff);

    /// <summary>注册单位配置，同 ConfigKey 覆盖。</summary>
    void RegisterUnit(UnitConfig unit);

    /// <summary>注册玩家可选单位：同一次调用写入单位表与玩家可选单位名册，同键覆盖单位，空键拒绝。</summary>
    void RegisterPlayerSelectableUnit(UnitConfig unit);

    /// <summary>注册副本配置，同 DungeonKey 覆盖，空键拒绝。</summary>
    void RegisterDungeon(DungeonConfig dungeon);
}

