namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// 由 mod 侧数据提供的一切相对路径的唯一安全闸：manifest 声明的产物路径与 mod 声明的资源寻址共用同一规则。
/// 只接受 <c>/</c> 分隔的包内相对路径；绝对路径、盘符、UNC、以分隔符开头、含上级跳转、任一段含冒号一律拒绝。
/// 冒号两侧文件系统语义不同，一并拒绝可保证两端同一清单同一裁决。
/// </summary>
public static class ModRelativePath {
    /// <summary>是否为可接受的包内相对路径。</summary>
    public static bool IsSafe(string? relative) {
        if (string.IsNullOrEmpty(relative) || relative[0] is '/' or '\\')
            return false;
        if (Path.IsPathRooted(relative) || relative.Contains("..", StringComparison.Ordinal))
            return false;
        return !relative.Contains(':', StringComparison.Ordinal);
    }

    /// <summary>解析为 rootDirectory 内的绝对路径；非法或解析后越出该目录返回 false。</summary>
    public static bool TryResolve(string rootDirectory, string? relative, out string? absolutePath) {
        absolutePath = null;
        if (!IsSafe(relative))
            return false;

        string root = Path.GetFullPath(rootDirectory);
        string candidate = Path.GetFullPath(
            Path.Combine(root, relative!.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return false;

        absolutePath = candidate;
        return true;
    }
}
