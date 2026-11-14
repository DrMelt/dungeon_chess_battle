using DungeonChessBattle.Battle.Mod.Manager;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 一次扫描的展示声明集合：读启用与停用 mod 的展示段，产出装配用的声明序与查询用的归属索引。
/// 停用者也读，供管理面显示有无展示代码。
/// </summary>
internal sealed class ModDisplaySet(
    Dictionary<string, ModDisplayDeclaration> byModId,
    IReadOnlyList<ModDisplayDeclaration> enabled,
    IReadOnlyList<ModError> declarationErrors) {
    /// <summary>参与装载且声明可用的启用 mod 声明，顺序与 <see cref="ModLoadResult.Mods"/> 一致。</summary>
    public IReadOnlyList<ModDisplayDeclaration> Enabled { get; } = enabled;

    /// <summary>展示段写错、缺字段或路径非法产生的错误。</summary>
    public IReadOnlyList<ModError> DeclarationErrors { get; } = declarationErrors;

    /// <summary>读启用集与停用集内每个 mod 的展示声明，声明不可用与段内问题逐条落日志。</summary>
    public static ModDisplaySet Read(ModLoadResult load, ILogger logger) {
        var errors = new List<ModError>();
        var byModId = new Dictionary<string, ModDisplayDeclaration>(StringComparer.Ordinal);
        foreach (var mod in load.Mods.Concat(load.Disabled)) {
            var read = ModDisplayDeclarationReader.Read(mod, errors);
            if (read.IsError) {
                errors.Add(new ModError(mod.Manifest.Id, read.FirstError.Description));
                continue;
            }

            byModId[mod.Manifest.Id] = read.Value;
        }

        if (logger.IsEnabled(LogLevel.Error))
            foreach (var error in errors)
                logger.LogError("展示声明读取失败：{ModId}：{Reason}", error.ModId, error.Message);

        // 声明不可用的 mod 不进启用序，原因已在 DeclarationErrors
        var enabled = new List<ModDisplayDeclaration>();
        foreach (var mod in load.Mods)
            if (byModId.TryGetValue(mod.Manifest.Id, out var display))
                enabled.Add(display);

        return new ModDisplaySet(byModId, enabled, errors);
    }

    /// <summary>该 mod 是否声明了展示入口 DLL；被拒载的目录无声明记录，恒为 false。</summary>
    public bool HasEntryCode(string modId) =>
        byModId.TryGetValue(modId, out var display) && display.EntryDlls.Count > 0;
}
