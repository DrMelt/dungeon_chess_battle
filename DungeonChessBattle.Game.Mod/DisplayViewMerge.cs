using DungeonChessBattle.Game.Shared;
using Godot;

namespace DungeonChessBattle.Game.Mod;

/// <summary>
/// 同键视图的字段级合并：后注册者声明了什么就改什么，未声明字段沿用被覆盖者。
/// mod 只提供图标时不会把内置的名称与描述一并抹掉，这是「mod 改写内置展示」的应有形状。
/// </summary>
internal static class ViewMerge {
    /// <summary>字符串成员取后注册者的值，空串视为未声明。</summary>
    internal static string TextOr(string next, string previous) => string.IsNullOrEmpty(next) ? previous : next;
}

internal sealed class SkillViewMerge(ISkillView previous, ISkillView next) : ISkillView {
    internal static ISkillView Merge(ISkillView previous, ISkillView next) => new SkillViewMerge(previous, next);

    public string Id => next.Id;
    public string Name => ViewMerge.TextOr(next.Name, previous.Name);
    public string Description => ViewMerge.TextOr(next.Description, previous.Description);
    public Texture2D? Icon => next.Icon ?? previous.Icon;
    public PackedScene? ApplyEffectScene => next.ApplyEffectScene ?? previous.ApplyEffectScene;
    public PackedScene? RangeHintScene => next.RangeHintScene ?? previous.RangeHintScene;
}

internal sealed class BuffViewMerge(IBuffView previous, IBuffView next) : IBuffView {
    internal static IBuffView Merge(IBuffView previous, IBuffView next) => new BuffViewMerge(previous, next);

    public ushort BuffTypeId => next.BuffTypeId;
    public string Name => ViewMerge.TextOr(next.Name, previous.Name);
    public string Description => ViewMerge.TextOr(next.Description, previous.Description);
    public Texture2D? Icon => next.Icon ?? previous.Icon;
}

internal sealed class UnitViewMerge(IUnitView previous, IUnitView next) : IUnitView {
    internal static IUnitView Merge(IUnitView previous, IUnitView next) => new UnitViewMerge(previous, next);

    public string ConfigKey => next.ConfigKey;
    public string DisplayName => ViewMerge.TextOr(next.DisplayName, previous.DisplayName);
    public string Description => ViewMerge.TextOr(next.Description, previous.Description);
    public Texture2D? Icon => next.Icon ?? previous.Icon;
    public PackedScene? ModelScene => next.ModelScene ?? previous.ModelScene;
    public Color? BodyColor => next.BodyColor ?? previous.BodyColor;
}

internal sealed class DungeonViewMerge(IDungeonView previous, IDungeonView next) : IDungeonView {
    internal static IDungeonView Merge(IDungeonView previous, IDungeonView next) =>
        new DungeonViewMerge(previous, next);

    public string Key => next.Key;
    public string DisplayName => ViewMerge.TextOr(next.DisplayName, previous.DisplayName);
    public string Description => ViewMerge.TextOr(next.Description, previous.Description);
    public PackedScene? EnvScene => next.EnvScene ?? previous.EnvScene;
}
