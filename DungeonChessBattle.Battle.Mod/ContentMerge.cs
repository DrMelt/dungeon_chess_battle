using DungeonChessBattle.Battle.Mod.Content;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// mod 内容 JSON 的键级合并：按优先级升序依次并入，同键后者覆盖前者。
/// BuffTypeId 为跨端同步数值身份，跨键冲突直接抛异常拦下，杜绝运行时静默错位。
/// </summary>
public static class ContentMerge {
    /// <summary>引擎内置 BuffTypeId 段上限，内置内容与 mod 不得越段声明。</summary>
    public const ushort BuiltinBuffTypeIdMax = 999;

    /// <summary>批量合并多个来源，source 顺序即加载顺序，后者覆盖前者同键内容。</summary>
    public static ModContentJson Merge(IReadOnlyList<ModContentJson> sources) {
        var merged = new ModContentJson();
        foreach (var source in sources)
            MergeInto(merged, source);
        return merged;
    }

    /// <summary>把 source 并入 target；同键内容由 source 覆盖。BuffTypeId 跨键重复抛异常。</summary>
    public static void MergeInto(ModContentJson target, ModContentJson source) {
        foreach (var skill in source.Skills)
            ReplaceById(target.Skills, skill, s => s.Id);
        foreach (var buff in source.Buffs) {
            ReplaceById(target.Buffs, buff, b => b.Id);
            EnsureBuffTypeIdUnique(target.Buffs, buff.Id, buff.BuffTypeId);
        }
        foreach (var unit in source.Units)
            ReplaceById(target.Units, unit, u => u.ConfigKey);
        foreach (var dungeon in source.Dungeons)
            ReplaceById(target.Dungeons, dungeon, d => d.Key);
        if (source.DefaultDungeonKey is not null)
            target.DefaultDungeonKey = source.DefaultDungeonKey;
    }

    /// <summary>mod 内容专用合并：额外校验 BuffTypeId 必须落在 mod 段。</summary>
    public static ModContentJson MergeModContent(IReadOnlyList<ModContentJson> sources) {
        foreach (var source in sources)
            foreach (var buff in source.Buffs)
                if (buff.BuffTypeId <= BuiltinBuffTypeIdMax)
                    throw new InvalidOperationException(
                        $"mod Buff '{buff.Id}' 的 BuffTypeId {buff.BuffTypeId} 落在引擎内置段（1~{BuiltinBuffTypeIdMax}），mod 必须声明 {BuiltinBuffTypeIdMax + 1} 及以上");
        return Merge(sources);
    }

    private static void ReplaceById<T>(List<T> list, T item, Func<T, string> keyOf) {
        int index = list.FindIndex(it => string.Equals(keyOf(it), keyOf(item), StringComparison.Ordinal));
        if (index >= 0)
            list[index] = item;
        else
            list.Add(item);
    }

    private static void EnsureBuffTypeIdUnique(List<BuffContent> buffs, string id, ushort typeId) {
        if (typeId == 0)
            throw new InvalidOperationException($"Buff '{id}' 必须声明非零 BuffTypeId");
        var conflicting = buffs.FirstOrDefault(b => b.BuffTypeId == typeId && !string.Equals(b.Id, id, StringComparison.Ordinal));
        if (conflicting is not null)
            throw new InvalidOperationException(
                $"BuffTypeId {typeId} 冲突：'{conflicting.Id}' 与 '{id}'，全量内容内 BuffTypeId 必须唯一");
    }
}
