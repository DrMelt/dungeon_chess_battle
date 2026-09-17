using DungeonChessBattle.Battle.Mod.Manager;
using ErrorOr;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 展示面声明读取的可预期失败：清单读不出来、展示段不可解析或缺必填字段，三者都使该 mod 的展示面整体跳过。
/// 描述面向日志与管理面板，归属由消费侧在转换处按 mod 补上；段内单个条目非法不在此列，逐条记 <see cref="ModError"/>。
/// </summary>
public static class ModDisplayErrors {
    /// <summary>清单文件读不出来：文件被占用或权限不足。</summary>
    public static Error ManifestUnreadable(string reason) => Error.Failure(
        code: "ModDisplay.Manifest.Unreadable",
        description: $"{ModLayout.ManifestFileName} 展示段读取失败：{reason}");

    /// <summary>展示段不是对象或不可解析。</summary>
    public static Error SectionUnreadable(string reason) => Error.Validation(
        code: "ModDisplay.Section.Unreadable",
        description: $"manifest.{ModLayout.ManifestDisplaySection} 段不可解析，展示面未装配：{reason}");

    /// <summary>展示段缺少必填字段。</summary>
    public static Error SectionMissingFields(IEnumerable<string> missing) => Error.Validation(
        code: "ModDisplay.Section.MissingField",
        description: $"manifest.{ModLayout.ManifestDisplaySection} 缺少必填字段 {string.Join("、", missing)}；无该产物时写 []");
}
