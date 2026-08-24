using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Game.GamePlayUI.skill_list;

/// <summary>
/// 技能施放动作抽象：把已确定的施放意图提交给战斗服务。
/// 施放意图统一为扁平参数，本接口与网络载体解耦。
/// </summary>
public interface ISkillCaster {
    /// <summary>施放技能。</summary>
    /// <param name="skillKey">技能配置键。</param>
    /// <param name="targetNetId">目标单位网络 ID，无目标传 0。</param>
    /// <param name="posX">位置目标 X，非位置技能传 0。</param>
    /// <param name="posZ">位置目标 Z，非位置技能传 0。</param>
    void Cast(SkillKeyId skillKey, ushort targetNetId, float posX, float posZ);
}
