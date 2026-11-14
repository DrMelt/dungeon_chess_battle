using System.Security.Cryptography;
using System.Text;
using ErrorOr;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>内容指纹计算：稳定地把 mod 集合映射为十六进制摘要。</summary>
public static class ContentFingerprint {
    /// <summary>
    /// 按加载顺序对每个 mod 取 Id / Version / Revision / CodeHash 拼接做 SHA-256。
    /// CodeHash 入摘要，内容改动必然引起指纹变化。展示面不在范围内，两端展示不同不破坏确定性。
    /// </summary>
    /// <remarks>无 mod 返回空串：使 <c>DataRevision</c> 在无 mod 时恒等于引擎内容修订号。</remarks>
    public static string Compute(IReadOnlyList<LoadedMod> mods) {
        if (mods.Count == 0)
            return "";

        var builder = new StringBuilder();
        foreach (var mod in mods) {
            builder.Append(mod.Manifest.Id);
            builder.Append('|');
            builder.Append(mod.Manifest.Version);
            builder.Append('|');
            builder.Append(mod.Manifest.Revision);
            builder.Append('|');
            builder.Append(mod.CodeHash);
            builder.Append('\n');
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    /// <summary>计算文件字节的 SHA-256 十六进制摘要；读取失败以错误返回，原因取自文件系统。</summary>
    private static ErrorOr<string> TryHashFile(string absolutePath) {
        try {
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolutePath)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return ModLoaderErrors.ArtifactUnreadable(absolutePath, ex.Message);
        }
    }

    /// <summary>
    /// 计算 mod 数据面指纹：入口文件与各探测目录顶层 DLL 取并集，按「文件名|字节摘要」Ordinal 排序去重后整体摘要。
    /// 传入的集合必须与装载侧解析到的是同一份，否则未被哈希的 DLL 会成为门控缺口；无 DLL 时返回空串。
    /// 产物与探测目录读取失败都按错误返回：摘要算不出即拒载整个 mod。
    /// </summary>
    public static ErrorOr<string> HashCodeFiles(
        IReadOnlyList<string> entryFiles, IReadOnlyList<string> probeDirectories) {
        var digests = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in entryFiles.Where(File.Exists)) {
            var digest = TryHashFile(file);
            if (digest.IsError)
                return digest.FirstError;
            digests.Add($"{Path.GetFileName(file)}|{digest.Value}");
        }

        foreach (string directory in probeDirectories) {
            if (!Directory.Exists(directory))
                continue;

            string[] libraries;
            try {
                libraries = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                return ModLoaderErrors.DirectoryUnreadable(directory, ex.Message);
            }

            foreach (string library in libraries) {
                var digest = TryHashFile(library);
                if (digest.IsError)
                    return digest.FirstError;
                digests.Add($"{Path.GetFileName(library)}|{digest.Value}");
            }
        }

        if (digests.Count == 0)
            return "";

        byte[] hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', digests.Order(StringComparer.Ordinal))));
        return Convert.ToHexString(hash);
    }
}
