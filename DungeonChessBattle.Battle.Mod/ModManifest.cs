namespace DungeonChessBattle.Battle.Mod;

/// <summary>manifest.json 文件结构，camelCase 键。</summary>
public sealed class ModManifestJson {
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
}

/// <summary>已校验的 mod 清单领域对象。</summary>
public sealed record ModManifest(
    string Id,
    string Name,
    string Version,
    string Revision,
    IReadOnlyList<string> Dependencies,
    int Priority);
