namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 装载与装配期的一条错误：归属 mod ID 与原因。归属随错误一起产出，
/// 消费方按 ID 精确分流，不从错误文本前缀解析。
/// </summary>
/// <param name="ModId">错误归属的 mod ID；清单本身解析失败时为目录名。</param>
/// <param name="Message">原因文本，面向日志与管理面板。</param>
public sealed record ModError(string ModId, string Message) {
    /// <summary>以「modId: 原因」呈现，供日志与列表直接消费。</summary>
    public override string ToString() => $"{ModId}: {Message}";
}
