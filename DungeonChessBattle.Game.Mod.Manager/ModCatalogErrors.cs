using ErrorOr;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// mod 管理面动作的可预期失败：目标 mod 不在当前扫描结果内。原因面向管理面提示，
/// 启用集本身读写失败的原因由数据面 mod 管理库给出。
/// </summary>
public static class ModCatalogErrors {
    /// <summary>目标 mod 不在当前扫描结果内，无从改写启用集。</summary>
    public static Error NotInScan(string modId) => Error.Validation(
        code: "ModCatalog.NotInScan", description: $"{modId} 不在当前扫描结果内");
}
