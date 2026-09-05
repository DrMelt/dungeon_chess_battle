namespace DungeonChessBattle.Battle.Mod.Content;

/// <summary>content.json 文件结构，camelCase 键。未声明的数组字段视为空，未声明的可空字段视为未配置。</summary>
public sealed class ModContentJson {
    /// <summary>技能定义表，按 Id 合并覆盖。</summary>
    public List<SkillContent> Skills { get; set; } = [];

    /// <summary>Buff 定义表，按 Id 合并覆盖。</summary>
    public List<BuffContent> Buffs { get; set; } = [];

    /// <summary>单位定义表，按 ConfigKey 合并覆盖。</summary>
    public List<UnitContent> Units { get; set; } = [];

    /// <summary>副本定义表，按 Key 合并覆盖。</summary>
    public List<DungeonContent> Dungeons { get; set; } = [];

    /// <summary>覆盖默认副本键；未声明沿用基座。</summary>
    public string? DefaultDungeonKey {
        get; set;
    }
}

/// <summary>技能定义。Kind 决定生效的行为子类型：damage / heal / range_damage / add_buff / hate。</summary>
public sealed class SkillContent {
    /// <summary>技能键，与 SkillKeyId 对齐，全链路字符串。</summary>
    public string Id { get; set; } = "";

    /// <summary>技能子类型。</summary>
    public string Kind { get; set; } = "damage";

    /// <summary>读条时长秒。</summary>
    public float SpellTime {
        get; set;
    }

    /// <summary>个体冷却秒。</summary>
    public float CooldownTime {
        get; set;
    }

    /// <summary>全局冷却；未配置使用默认分组。GroupKey 为空表示不参与全局冷却。</summary>
    public GcdContent? Gcd {
        get; set;
    }

    /// <summary>是否需要指定单位目标。</summary>
    public bool NeedUnitTarget {
        get; set;
    }

    /// <summary>是否需要指定位置目标。</summary>
    public bool NeedPosTarget {
        get; set;
    }

    /// <summary>可释放目标类型：None / Same / Different。</summary>
    public string TargetPolicy { get; set; } = "Different";

    /// <summary>最大施法距离，0 表示不限制。</summary>
    public float CastRange {
        get; set;
    }

    /// <summary>技能效果行为 ID；为空使用该 Kind 的默认内置行为。</summary>
    public string Effect { get; set; } = "";

    /// <summary>伤害值，damage / range_damage 使用。</summary>
    public float? Damage {
        get; set;
    }

    /// <summary>伤害类型。</summary>
    public string DamageType { get; set; } = "None";

    /// <summary>治疗量，heal 使用。</summary>
    public float? CurePotency {
        get; set;
    }

    /// <summary>仇恨操作类型，hate 使用。</summary>
    public string HateOp { get; set; } = "SetTop";

    /// <summary>仇恨操作数值，hate 使用。</summary>
    public float HateValue {
        get; set;
    }

    /// <summary>施加的 Buff 键，add_buff 使用；引用 Buffs 表的 Id。</summary>
    public string Buff { get; set; } = "";

    /// <summary>位置目标的有效范围形状，range_damage 使用。</summary>
    public RangeAreaContent? CastArea {
        get; set;
    }
}

/// <summary>技能全局冷却声明。</summary>
public sealed class GcdContent {
    /// <summary>冷却组键；空表示不参与全局冷却。</summary>
    public string? GroupKey {
        get; set;
    }

    /// <summary>冷却时长秒。</summary>
    public float Time { get; set; } = 2.5f;
}

/// <summary>位置目标有效范围形状声明。</summary>
public sealed class RangeAreaContent {
    /// <summary>形状：rect / sector。</summary>
    public string Shape { get; set; } = "rect";

    /// <summary>近端沿朝向边界；sector 为近端半径。</summary>
    public float NearClamp {
        get; set;
    }

    /// <summary>远端沿朝向边界；sector 为远端半径。</summary>
    public float FarClamp {
        get; set;
    }

    /// <summary>rect 左侧横向边界。</summary>
    public float FromLeft { get; set; } = -1f;

    /// <summary>rect 右侧横向边界。</summary>
    public float ToRight { get; set; } = 1f;

    /// <summary>sector 扇形起始角，弧度，以朝向为 0。</summary>
    public float RadianFrom { get; set; } = -MathF.PI;

    /// <summary>sector 扇形结束角，弧度，以朝向为 0。</summary>
    public float RadianTo { get; set; } = MathF.PI;
}

/// <summary>Buff 定义。Kind 决定生效的行为子类型：dot / hot。</summary>
public sealed class BuffContent {
    /// <summary>Buff 键，被技能的 Buff 字段引用。</summary>
    public string Id { get; set; } = "";

    /// <summary>BuffTypeId，跨端同步的 ushort 数值 ID。引擎内置段 1~999，mod 必须声明 1000 及以上。</summary>
    public ushort BuffTypeId {
        get; set;
    }

    /// <summary>Buff 子类型。</summary>
    public string Kind { get; set; } = "dot";

    /// <summary>持续时间秒。</summary>
    public double Duration {
        get; set;
    }

    /// <summary>最大叠加层数。</summary>
    public int MaxStacks { get; set; } = 1;

    /// <summary>伤害类型。</summary>
    public string DamageType { get; set; } = "None";

    /// <summary>每秒伤害，dot 使用。</summary>
    public float DamagePerSec {
        get; set;
    }

    /// <summary>每秒治疗，hot 使用。</summary>
    public float HealthPerSec {
        get; set;
    }

    /// <summary>Buff 持续效果行为 ID；为空使用该 Kind 的默认内置行为。</summary>
    public string Effect { get; set; } = "";
}
