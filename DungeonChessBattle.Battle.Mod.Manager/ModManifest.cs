using System.Text.Json.Serialization;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// manifest.json 文件结构，camelCase 键。未知键即拒载：路径字段写错键名会静默回落到默认目录，
/// 表现是「配了没生效」，比当场报错难查得多。
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ModManifestJson {
    /// <summary>mod 唯一 ID，同时是 mods 根目录下的目录名。</summary>
    public string Id { get; set; } = "";

    /// <summary>展示名。</summary>
    public string Name { get; set; } = "";

    /// <summary>语义版本号。</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>该 mod 的内容修订号，内容变更时递增，参与内容指纹。</summary>
    public string Revision { get; set; } = "0";

    /// <summary>依赖的其他 mod ID，按顺序加载。</summary>
    public List<string> Dependencies { get; set; } = [];

    /// <summary>覆盖优先级，数值大者后加载并覆盖先加载的同键内容。</summary>
    public int Priority { get; set; } = 10;

    /// <summary>数据入口 DLL，相对 mod 目录，数组顺序即装载顺序；null 即回落 <c>code/*.dll</c>。</summary>
    public List<string>? Code {
        get; set;
    }

    /// <summary>数据面额外的依赖探测目录；入口 DLL 所在目录自动附加，依赖与入口同目录时无需声明。</summary>
    public List<string>? CodeLibraries {
        get; set;
    }

    /// <summary>展示入口 DLL，仅客户端装载；null 即回落 <c>code_display/*.dll</c>。</summary>
    public List<string>? CodeDisplay {
        get; set;
    }

    /// <summary>展示面额外的依赖探测目录，不进内容指纹。</summary>
    public List<string>? CodeDisplayLibraries {
        get; set;
    }

    /// <summary>待挂载的展示资源包；null 即回落 <c>assets/*.pck</c>。</summary>
    public List<string>? Packages {
        get; set;
    }
}

/// <summary>
/// 已校验的 mod 清单领域对象。路径字段一律是相对 mod 目录的路径，未定位也未验存在性：
/// 入口与资源包未声明时已按默认目录枚举补齐，探测目录原样保留声明、未声明即空。
/// 被拒载的目录同样带着它进管理面，清单必须自足。绝对路径见 <see cref="LoadedMod"/>。
/// </summary>
public sealed record ModManifest(
    string Id,
    string Name,
    string Version,
    string Revision,
    IReadOnlyList<string> Dependencies,
    int Priority,
    IReadOnlyList<string> Code,
    IReadOnlyList<string> CodeLibraries,
    IReadOnlyList<string> CodeDisplay,
    IReadOnlyList<string> CodeDisplayLibraries,
    IReadOnlyList<string> Packages);
