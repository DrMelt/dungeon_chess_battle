namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 技能全局冷却配置：所属组键与时长。组键为空表示不参与全局冷却。
/// 未显式配置的技能一律使用默认分组 <see cref="Default"/>。
/// </summary>
public sealed class GcdDefinition {
    /// <summary>默认全局冷却：默认分组键与 2.5 秒时长。</summary>
    public static readonly GcdDefinition Default = new() {
        GroupKey = "default",
        Time = 2.5f,
    };

    /// <summary>全局冷却组键，同一组键共享一条全局冷却通道、组间互不阻塞；空值表示不参与全局冷却。</summary>
    public required string? GroupKey {
        get; init;
    }

    /// <summary>全局冷却时长，秒。</summary>
    public required float Time {
        get; init;
    }
}
