namespace DungeonChessBattle.Battle.Mod.Content;

/// <summary>单位定义。</summary>
public sealed class UnitContent {
    /// <summary>单位配置键，全链路字符串身份。</summary>
    public string ConfigKey { get; set; } = "";

    /// <summary>是否可被玩家在准备阶段选择；敌人单位设为 false。</summary>
    public bool IsPlayerSelectable { get; set; } = true;

    /// <summary>最大生命值。</summary>
    public float MaxHealth {
        get; set;
    }

    /// <summary>碰撞半径。</summary>
    public float BodyRadius {
        get; set;
    }

    /// <summary>基础移动速度。</summary>
    public float BaseSpeed {
        get; set;
    }

    /// <summary>物理攻击基础系数。</summary>
    public float PhysicalAttackBase {
        get; set;
    }

    /// <summary>物理伤害承受系数。</summary>
    public float PhysicalTakePercent {
        get; set;
    }

    /// <summary>魔法攻击基础系数。</summary>
    public float MagicAttackBase {
        get; set;
    }

    /// <summary>魔法伤害承受系数。</summary>
    public float MagicTakePercent {
        get; set;
    }

    /// <summary>治疗强度系数。</summary>
    public float CureIntensity {
        get; set;
    }

    /// <summary>拥有的技能键列表，引用 Skills 表的 Id。</summary>
    public List<string> Skills { get; set; } = [];

    /// <summary>敌人决策行为 ID；为空表示玩家单位无 AI。</summary>
    public string Intelligence { get; set; } = "";

    /// <summary>仇恨规则行为 ID；为空表示不参与仇恨计算。</summary>
    public string HateRule { get; set; } = "";

    /// <summary>仇恨生成倍率，默认 1.0。</summary>
    public float HateFactor { get; set; } = 1f;
}

/// <summary>副本定义。</summary>
public sealed class DungeonContent {
    /// <summary>副本键。</summary>
    public string Key { get; set; } = "";

    /// <summary>玩家阵营选项列表。</summary>
    public List<PlayerCampOptionContent> PlayerCamps { get; set; } = [];

    /// <summary>敌方阵营列表。</summary>
    public List<string> EnemyCamps { get; set; } = [];

    /// <summary>敌人生成阵容。</summary>
    public List<EnemySpawnContent> Enemies { get; set; } = [];

    /// <summary>阵营关系行为 ID，默认 pve_boss。</summary>
    public string Relations { get; set; } = "pve_boss";

    /// <summary>战场布局；未配置使用默认竞技场。</summary>
    public LayoutContent? Layout {
        get; set;
    }
}

/// <summary>玩家阵营选项：客户端提交选项键，服务端解析实际阵营列表。</summary>
public sealed class PlayerCampOptionContent {
    /// <summary>选项键。</summary>
    public string Key { get; set; } = "";

    /// <summary>该选项对应的阵营列表。</summary>
    public List<string> Camps { get; set; } = [];
}

/// <summary>敌人阵容条目。</summary>
public sealed class EnemySpawnContent {
    /// <summary>单位键，引用 Units 表的 ConfigKey。</summary>
    public string Unit { get; set; } = "";

    /// <summary>生成数量。</summary>
    public int Count { get; set; } = 1;

    /// <summary>出生列基准 X。</summary>
    public float SpawnBaseX { get; set; } = 30f;

    /// <summary>同批出生间距。</summary>
    public float SpawnXSpacing { get; set; } = 3f;
}

/// <summary>战场布局声明。</summary>
public sealed class LayoutContent {
    /// <summary>竞技场半宽。</summary>
    public float HalfWidth { get; set; } = 50f;

    /// <summary>竞技场半高。</summary>
    public float HalfHeight { get; set; } = 30f;

    /// <summary>静态障碍矩形。</summary>
    public List<ObstacleContent> Obstacles { get; set; } = [];
}

/// <summary>静态障碍矩形声明。</summary>
public sealed class ObstacleContent {
    /// <summary>左边界 X。</summary>
    public float MinX {
        get; set;
    }

    /// <summary>下边界 Y。</summary>
    public float MinY {
        get; set;
    }

    /// <summary>右边界 X。</summary>
    public float MaxX {
        get; set;
    }

    /// <summary>上边界 Y。</summary>
    public float MaxY {
        get; set;
    }
}
