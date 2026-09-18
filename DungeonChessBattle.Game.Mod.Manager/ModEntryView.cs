using DungeonChessBattle.Battle.Mod.Manager;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>mods 根目录内单个 mod 的管理视图，供管理面列示。</summary>
public sealed class ModEntryView {
    /// <summary>mod ID，同时是 mods 根目录下的子目录名。</summary>
    public required string Id {
        get; init;
    }

    /// <summary>语义版本号。</summary>
    public required string Version {
        get; init;
    }

    /// <summary>是否处于启用集内。停用不改目录内容，只把它排除出装配。</summary>
    public required bool IsEnabled {
        get; init;
    }

    /// <summary>mod 目录绝对路径。</summary>
    public required string DirectoryPath {
        get; init;
    }

    /// <summary>声明的依赖 mod ID。</summary>
    public required IReadOnlyList<string> Dependencies {
        get; init;
    }

    /// <summary>是否声明了数据入口 DLL。</summary>
    public required bool HasCode {
        get; init;
    }

    /// <summary>是否声明了展示入口 DLL。</summary>
    public required bool HasDisplayCode {
        get; init;
    }

    /// <summary>本 mod 名下的装载错误；空表示无错。</summary>
    public required IReadOnlyList<ModError> Errors {
        get; init;
    }

    /// <summary>未装载原因；非 null 表示该目录被拒载或解析失败，不参与装配。</summary>
    public string? Reason {
        get; init;
    }
}
