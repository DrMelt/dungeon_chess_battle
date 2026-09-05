namespace DungeonChessBattle.Game.Mod;

/// <summary>
/// godot_assets.json 文件结构：mod 包内展示层数据，仅 Godot 端装配读取，纯 .NET 数据面忽略。
/// 图标是 mod 内 images 目录下的文件相对名，场景/特效引用引擎预置资产 ID（未配置可空）。
/// </summary>
public sealed class GodotAssetsJson {
    /// <summary>技能展示数据，键与 Skills 表的 Id 对齐。</summary>
    public List<SkillAssetContent> Skills { get; set; } = [];

    /// <summary>Buff 展示数据，键与 Buffs 表的 Id 对齐。</summary>
    public List<BuffAssetContent> Buffs { get; set; } = [];

    /// <summary>副本展示数据，键与 Dungeons 表的 Key 对齐。</summary>
    public List<DungeonAssetContent> Dungeons { get; set; } = [];
}

/// <summary>技能展示数据。</summary>
public sealed class SkillAssetContent {
    /// <summary>技能键，与 content.json 的 SkillId 对齐。</summary>
    public string Id { get; set; } = "";

    /// <summary>图标文件名（images 目录下相对路径）。</summary>
    public string? Icon {
        get; set;
    }

    /// <summary>技能名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>技能描述。</summary>
    public string Description { get; set; } = "";

    /// <summary>施放特效资产 ID，引用引擎预置资产。</summary>
    public string? ApplyEffect {
        get; set;
    }

    /// <summary>范围提示资产 ID，引用引擎预置资产。</summary>
    public string? RangeHint {
        get; set;
    }
}

/// <summary>Buff 展示数据。</summary>
public sealed class BuffAssetContent {
    /// <summary>Buff 键，与 content.json 的 BuffId 对齐。</summary>
    public string Id { get; set; } = "";

    /// <summary>图标文件名（images 目录下相对路径）。</summary>
    public string? Icon {
        get; set;
    }

    /// <summary>Buff 名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>Buff 描述。</summary>
    public string Description { get; set; } = "";
}

/// <summary>副本展示数据。</summary>
public sealed class DungeonAssetContent {
    /// <summary>副本键，与 content.json 的 DungeonKey 对齐。</summary>
    public string Key { get; set; } = "";

    /// <summary>显示名。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>展示描述。</summary>
    public string Description { get; set; } = "";

    /// <summary>地面主题色，CSS 十六进制，如 #44556688。</summary>
    public string? GroundColor {
        get; set;
    }

    /// <summary>天空主题色。</summary>
    public string? SkyColor {
        get; set;
    }

    /// <summary>方向光补光色。</summary>
    public string? LightColor {
        get; set;
    }

    /// <summary>环境场景资产 ID，引用引擎预置资产。</summary>
    public string? EnvScene {
        get; set;
    }
}
