using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// manifest.json 的文件结构，camelCase 键。未知键与缺失必填字段一律拒载，必填字段见 <see cref="ModLoader"/> 的校验。
/// 本类只描述数据面，展示面声明段按 <see cref="ModLayout.ManifestDisplaySection"/> 登记键的存在。
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ModManifestJson {
    /// <summary>mod 唯一 ID，同时是 mods 根目录下的目录名；必填。</summary>
    public string? Id {
        get; set;
    }

    /// <summary>语义版本号，进内容指纹；必填。</summary>
    public string? Version {
        get; set;
    }

    /// <summary>该 mod 的内容修订号，内容变更时递增，进内容指纹；必填。</summary>
    public string? Revision {
        get; set;
    }

    /// <summary>依赖的其他 mod ID，按顺序加载；未声明即无依赖。</summary>
    public List<string> Dependencies { get; set; } = [];

    /// <summary>数据入口 DLL，相对 mod 目录，数组顺序即装载顺序；必填，无数据代码时写 <c>[]</c>。</summary>
    public List<string>? Code {
        get; set;
    }

    /// <summary>数据面额外的依赖探测目录；入口 DLL 所在目录自动附加，依赖与入口同目录时无需声明。</summary>
    public List<string>? CodeLibraries {
        get; set;
    }

    /// <summary>
    /// 展示面声明段。登记它才能使上面的「未知键拒载」不把展示段读成写错的键；
    /// 段内容归 Game.Mod.Manager，数据面不读也不对外传递。
    /// </summary>
    [JsonPropertyName(ModLayout.ManifestDisplaySection)]
    public JsonElement? Display {
        get; set;
    }
}

/// <summary>
/// 已校验的 mod 清单领域对象，只描述数据面。路径字段一律是相对 mod 目录的路径，未定位也未验存在性，
/// 入口由声明还原为相对路径供管理面判定有无数据代码；绝对路径见 <see cref="LoadedMod"/>。
/// </summary>
public sealed record ModManifest(
    string Id,
    string Version,
    string Revision,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Code);
