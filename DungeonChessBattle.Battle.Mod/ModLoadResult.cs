namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// 已加载的单个 mod：清单、目录、代码摘要。内容不在此——内容以领域对象在引导装配期注册，无中间文件。</summary>
public sealed class LoadedMod {
    /// <summary>manifest.json 校验后的清单。</summary>
    public required ModManifest Manifest {
        get; init;
    }

    /// <summary>mod 所在绝对目录。</summary>
    public required string DirectoryPath {
        get; init;
    }

    /// <summary>code 目录全部 DLL 的稳定摘要；无代码目录为空串。代码 mod 重新编译即指纹变化。</summary>
    public string CodeHash {
        get; init;
    } = "";
}

/// <summary>
/// 未参与装载的 mod 目录：清单解析失败、Id 无效或重复、依赖缺失/成环/被停用而被拒。
/// 只供管理面列示与定位，不参与内容装配，也不进内容指纹。
/// </summary>
public sealed class UnloadedMod {
    /// <summary>mod 所在绝对目录。</summary>
    public required string DirectoryPath {
        get; init;
    }

    /// <summary>解析出的清单；清单本身解析失败为 null，此时目录名是唯一身份。</summary>
    public ModManifest? Manifest {
        get; init;
    }

    /// <summary>未装载原因。</summary>
    public required string Reason {
        get; init;
    }
}

/// <summary>mod 目录扫描结果：参与装载的 mod、被停用的 mod、被拒载的目录、逐目录错误。错误不中断其余 mod。</summary>
public sealed class ModLoadResult {
    /// <summary>通过校验并完成依赖排序的启用 mod，按加载顺序排列。</summary>
    public required IReadOnlyList<LoadedMod> Mods {
        get; init;
    }

    /// <summary>因启用集而停用的 mod，无顺序含义，仅供管理面展示。</summary>
    public required IReadOnlyList<LoadedMod> Disabled {
        get; init;
    }

    /// <summary>未参与装载的 mod 目录，含被拒载者，仅供管理面列示。</summary>
    public required IReadOnlyList<UnloadedMod> Unloaded {
        get; init;
    }

    /// <summary>错误目录的说明；空表示全部成功。</summary>
    public required IReadOnlyList<string> Errors {
        get; init;
    }
}
