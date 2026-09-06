using System.Security.Cryptography;
using System.Text;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>内容指纹计算：稳定地把 mod 集合映射为十六进制摘要。</summary>
public static class ContentFingerprint {
    /// <summary>
    /// 按加载顺序对每个 mod 取 Id / Version / Revision / CodeHash 拼接做 SHA-256。
    /// 覆盖顺序源于加载顺序，故指纹必须按加载顺序计算而非按 Id 排序。
    /// 内容即代码：CodeHash 入摘要，改数值必须重编译数据 DLL，逃不过门控。
    /// 展示 DLL 不进指纹：展示字段不参与结算，两端展示不同不破坏确定性。
    /// </summary>
    /// <remarks>无 mod 返回空串：使 <c>DataRevision</c> 在无 mod 时恒等于基座修订号，与懒装配路径同值。</remarks>
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

    /// <summary>计算文件字节的 SHA-256 十六进制摘要；文件不存在抛异常，作为 loading 期响亮失败。</summary>
    public static string HashFile(string absolutePath) {
        byte[] hash = SHA256.HashData(File.ReadAllBytes(absolutePath));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 计算代码目录内全部 DLL 的稳定摘要：按文件名序拼「文件名|字节摘要」再整体摘要。
    /// 目录不存在返回空串，与「无代码 mod」同值。
    /// </summary>
    public static string HashCodeDirectory(string codeDirectory) {
        if (!Directory.Exists(codeDirectory))
            return "";

        var builder = new StringBuilder();
        foreach (string dll in Directory.GetFiles(codeDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal)) {
            builder.Append(Path.GetFileName(dll));
            builder.Append('|');
            builder.Append(HashFile(dll));
            builder.Append('\n');
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }
}
