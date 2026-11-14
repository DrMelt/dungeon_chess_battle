using ErrorOr;

namespace DungeonChessBattle.Battle.Config.Registry;

/// <summary>
/// 注册表写入的可预期失败：空身份键的写入一律拒绝。
/// 描述面向日志与管理面板，归属由调用侧按 mod 补上。
/// </summary>
public static class ContentRegistryErrors {
    /// <summary>技能定义未声明技能键。</summary>
    public static Error EmptySkillKey => Error.Validation(
        code: "ContentRegistry.SkillKeyEmpty", description: "技能必须声明技能键");

    /// <summary>Buff 定义未声明 Buff 键。</summary>
    public static Error EmptyBuffKey => Error.Validation(
        code: "ContentRegistry.BuffKeyEmpty", description: "Buff 必须声明 Buff 键");

    /// <summary>单位配置未声明配置键。</summary>
    public static Error EmptyUnitKey => Error.Validation(
        code: "ContentRegistry.UnitKeyEmpty", description: "单位必须声明配置键");

    /// <summary>副本配置未声明副本键。</summary>
    public static Error EmptyDungeonKey => Error.Validation(
        code: "ContentRegistry.DungeonKeyEmpty", description: "副本必须声明副本键");
}
