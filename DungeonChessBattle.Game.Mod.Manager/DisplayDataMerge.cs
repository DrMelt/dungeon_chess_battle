using DungeonChessBattle.Game.Shared.Display;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 同键展示数据的字段级合并：后写者声明了什么就改什么，未声明字段沿用被覆盖者。
/// mod 只提供图标时不会把内置的名称与描述一并抹掉，这是「mod 改写内置展示」的应有形状。
/// 空串与 null 即未声明，与 <c>Game.Shared</c> 的展示数据语义同一套。
/// </summary>
internal static class DisplayMerge {
    internal static SkillDisplay Merge(this SkillDisplay previous, SkillDisplay next) => previous with {
        Name = TextOr(next.Name, previous.Name),
        Description = TextOr(next.Description, previous.Description),
        Icon = next.Icon ?? previous.Icon,
        ApplyEffectScene = next.ApplyEffectScene ?? previous.ApplyEffectScene,
        RangeHintScene = next.RangeHintScene ?? previous.RangeHintScene,
    };

    internal static BuffDisplay Merge(this BuffDisplay previous, BuffDisplay next) => previous with {
        Name = TextOr(next.Name, previous.Name),
        Description = TextOr(next.Description, previous.Description),
        Icon = next.Icon ?? previous.Icon,
    };

    internal static UnitDisplay Merge(this UnitDisplay previous, UnitDisplay next) => previous with {
        DisplayName = TextOr(next.DisplayName, previous.DisplayName),
        Description = TextOr(next.Description, previous.Description),
        Icon = next.Icon ?? previous.Icon,
        ModelScene = next.ModelScene ?? previous.ModelScene,
        BodyColor = next.BodyColor ?? previous.BodyColor,
    };

    internal static DungeonDisplay Merge(this DungeonDisplay previous, DungeonDisplay next) => previous with {
        DisplayName = TextOr(next.DisplayName, previous.DisplayName),
        Description = TextOr(next.Description, previous.Description),
        EnvScene = next.EnvScene ?? previous.EnvScene,
    };

    /// <summary>字符串成员取后写者的值，空串视为未声明。</summary>
    private static string TextOr(string next, string previous) =>
        string.IsNullOrEmpty(next) ? previous : next;
}
