using DungeonChessBattle.Battle.Mod.Content;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>已加载的单个 mod：清单、目录、内容哈希待计算指纹的基本单位。</summary>
public sealed class LoadedMod {
    /// <summary>manifest.json 校验后的清单。</summary>
    public required ModManifest Manifest {
        get; init;
    }

    /// <summary>mod 所在绝对目录。</summary>
    public required string DirectoryPath {
        get; init;
    }

    /// <summary>content.json 原始字节 SHA-256 十六进制，内容变更即指纹变化。</summary>
    public required string ContentHash {
        get; init;
    }

    /// <summary>解析后的内容 JSON，合并与映射的数据源。</summary>
    public required ModContentJson Content {
        get; init;
    }
}

/// <summary>mod 目录扫描结果：可加载的 mod 列表与逐目录错误信息，错误不中断其余 mod。</summary>
public sealed class ModLoadResult {
    /// <summary>通过校验并完成依赖排序的 mod，按加载顺序排列。</summary>
    public required IReadOnlyList<LoadedMod> Mods {
        get; init;
    }

    /// <summary>错误目录的说明；空表示全部成功。</summary>
    public required IReadOnlyList<string> Errors {
        get; init;
    }
}

/// <summary>
/// 合并后的内容集：playable 内容唯一真相，由基座内容与全部启用 mod 按加载顺序合并而来。
/// 消费方经 ContentSet 构建领域注册表，不再直接读 mod 文件。
/// </summary>
public sealed class ContentSet {
    /// <summary>合并后的内容 JSON 根（skills/buffs/units/dungeons/defaultDungeonKey）。</summary>
    public required ModContentJson Content {
        get; init;
    }

    /// <summary>参与合并的 mod 列表，按实际加载顺序。</summary>
    public required IReadOnlyList<LoadedMod> Mods {
        get; init;
    }

    /// <summary>内容指纹：mod 清单与内容哈希的组合摘要，房间与回放一致性门控用。</summary>
    public required string Fingerprint {
        get; init;
    }
}
