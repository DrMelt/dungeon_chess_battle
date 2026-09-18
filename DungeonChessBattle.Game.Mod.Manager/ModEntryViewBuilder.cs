using DungeonChessBattle.Battle.Mod.Manager;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 管理列表条目构建：把一次扫描的装载结果与展示声明汇成按 ID 字母序的条目。
/// 启用、停用与被拒载的目录都出行，被拒载者按目录名立条目并带上拒载原因。
/// </summary>
internal static class ModEntryViewBuilder {
    /// <summary>用同一次扫描的装载结果与展示声明构建全部条目。</summary>
    public static IReadOnlyList<ModEntryView> Build(ModLoadResult load, ModDisplaySet displays) {
        var entries = new List<ModEntryView>();
        entries.AddRange(load.Mods.Select(mod => From(load, displays, mod, enabled: true)));
        entries.AddRange(load.Disabled.Select(mod => From(load, displays, mod, enabled: false)));
        entries.AddRange(load.Unloaded.Select(mod => From(load, mod)));
        entries.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        return entries;
    }

    private static ModEntryView From(
        ModLoadResult load, ModDisplaySet displays, LoadedMod mod, bool enabled) => new() {
            Id = mod.Manifest.Id,
            Version = mod.Manifest.Version,
            IsEnabled = enabled,
            DirectoryPath = mod.DirectoryPath,
            Dependencies = mod.Manifest.Dependencies,
            HasCode = mod.Manifest.Code.Count > 0,
            HasDisplayCode = displays.HasEntryCode(mod.Manifest.Id),
            Errors = [.. load.Errors.Where(error => error.ModId == mod.Manifest.Id)],
        };

    /// <summary>被拒载的目录不读展示声明，展示入口一栏恒为假。</summary>
    private static ModEntryView From(ModLoadResult load, UnloadedMod mod) {
        string directoryName = Path.GetFileName(mod.DirectoryPath);
        string id = mod.Manifest?.Id ?? directoryName;
        var error = load.Errors.FirstOrDefault(e => e.ModId == id) ?? new ModError(id, mod.Reason);
        return new ModEntryView {
            Id = id,
            Version = mod.Manifest?.Version ?? "",
            IsEnabled = false,
            DirectoryPath = mod.DirectoryPath,
            Dependencies = mod.Manifest?.Dependencies ?? [],
            HasCode = mod.Manifest?.Code.Count > 0,
            HasDisplayCode = false,
            Errors = [error],
            Reason = mod.Reason,
        };
    }
}
