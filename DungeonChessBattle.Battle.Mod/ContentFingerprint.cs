using System.Security.Cryptography;
using System.Text;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>内容指纹计算：稳定地把 mod 集合映射为十六进制摘要。</summary>
public static class ContentFingerprint {
    /// <summary>
    /// 按加载顺序对每个 mod 取 Id / Version / Revision / ContentHash 拼接做 SHA-256。
    /// 覆盖顺序源于加载顺序，故指纹必须按加载顺序计算而非按 Id 排序。
    /// </summary>
    public static string Compute(IReadOnlyList<LoadedMod> mods) {
        var builder = new StringBuilder();
        foreach (var mod in mods) {
            builder.Append(mod.Manifest.Id);
            builder.Append('|');
            builder.Append(mod.Manifest.Version);
            builder.Append('|');
            builder.Append(mod.Manifest.Revision);
            builder.Append('|');
            builder.Append(mod.ContentHash);
            builder.Append('\n');
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    /// <summary>计算文件字节的 SHA-256 十六进制摘要；文件不存在抛异常，作为 loading 期响亮失败。</summary>
    public static string HashFile(string absolutePath) {
        byte[] hash = SHA256.HashData(File.ReadAllBytes(absolutePath));
        return Convert.ToHexString(hash);
    }
}
