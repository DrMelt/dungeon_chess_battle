namespace DungeonChessBattle.Game.Shared;

/// <summary>
/// mod 包内资产的寻址键：所属 mod ID + mod 目录内的相对路径（如 <c>images/icon_a.png</c>）。
/// 相对路径一律以 <c>/</c> 分隔，寻址结果与平台无关，可跨端稳定比较。
/// </summary>
/// <param name="ModId">资产所属 mod 的 ID，即 manifest 与 mods 根目录下同名子目录。</param>
/// <param name="RelativePath">相对 mod 目录的资源路径，由展示数据声明，不含 mod 目录前缀。</param>
public readonly record struct ModAssetKey(string ModId, string RelativePath);
