namespace DungeonChessBattle.Battle.Shared;

/// <summary>
/// 行为注册表 ID 常量：宿主登记内置行为所用，mod 以相同 ID 覆盖或新 ID 扩展。
/// 行为实现本身经 <c>GameConfig.BehaviorCatalog</c> 注册，此处只建模，不含实现。
/// </summary>
public static class BehaviorIds {
    /// <summary>技能效果行为。</summary>
    public static class SkillEffect {
        /// <summary>单体伤害。</summary>
        public const string Damage = "skill.effect.damage";

        /// <summary>单体治疗。</summary>
        public const string Heal = "skill.effect.heal";

        /// <summary>施加 Buff。</summary>
        public const string AddBuff = "skill.effect.add_buff";

        /// <summary>仇恨操作。</summary>
        public const string Hate = "skill.effect.hate";

        /// <summary>范围伤害。</summary>
        public const string RangeDamage = "skill.effect.range_damage";
    }

    /// <summary>Buff 持续效果行为。</summary>
    public static class BuffEffect {
        /// <summary>持续伤害。</summary>
        public const string Dot = "buff.effect.dot";

        /// <summary>持续治疗。</summary>
        public const string Hot = "buff.effect.hot";
    }

    /// <summary>敌人决策行为。</summary>
    public static class Intelligence {
        /// <summary>基础敌人决策。</summary>
        public const string EnemyBasic = "ai.enemy_basic";
    }

    /// <summary>仇恨规则行为。</summary>
    public static class HateRule {
        /// <summary>默认仇恨规则。</summary>
        public const string Default = "hate.default";
    }

    /// <summary>阵营关系行为。</summary>
    public static class CampRelation {
        /// <summary>PvE Boss 敌对关系：双方同属 Boss 为友，任一含 Boss 为敌，其余按共同阵营。</summary>
        public const string PveBoss = "camp.pve_boss";
    }
}
