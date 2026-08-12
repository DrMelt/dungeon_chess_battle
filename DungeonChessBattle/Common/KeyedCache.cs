using System;
using System.Collections.Generic;

namespace DungeonChessBattle;

/// <summary>
/// 按键缓存，复用键对应的缓存对象，仅在源集合增删时创建或销毁。
/// </summary>
/// <typeparam name="TKey">缓存对象键类型。</typeparam>
/// <typeparam name="TItem">源数据条目类型。</typeparam>
/// <typeparam name="TValue">缓存对象类型。</typeparam>
/// <remarks>
/// 构造函数：注入键提取、创建、移除与更新回调。
/// </remarks>
/// <param name="getKey">从源条目提取键的回调。</param>
/// <param name="create">创建缓存对象的工厂回调。</param>
/// <param name="remove">移除缓存对象的回调。</param>
/// <param name="update">更新缓存对象的回调。</param>
public class KeyedCache<TKey, TItem, TValue>(
    Func<TItem, TKey> getKey,
    Func<TValue> create,
    Action<TValue> remove,
    Action<TValue, TItem> update)
    where TKey : notnull
    where TItem : notnull
    where TValue : class {
    /// <summary>键 → 缓存对象映射。</summary>
    private readonly Dictionary<TKey, TValue> _cache = [];

    /// <summary>从源条目提取键的回调。</summary>
    private readonly Func<TItem, TKey> _getKey = getKey;

    /// <summary>创建缓存对象的回调。</summary>
    private readonly Func<TValue> _create = create;

    /// <summary>移除缓存对象的回调。</summary>
    private readonly Action<TValue> _remove = remove;

    /// <summary>更新缓存对象的回调。</summary>
    private readonly Action<TValue, TItem> _update = update;

    /// <summary>
    /// 同步缓存集合：先创建新增缓存对象并清理已移除对象，最后统一更新全部缓存对象。
    /// </summary>
    /// <param name="source">数据源条目列表。</param>
    public void Sync(IReadOnlyList<TItem> source) {
        var newKeys = new HashSet<TKey>(source.Count);
        foreach (var item in source)
            newKeys.Add(_getKey(item));

        // 新增 = 新键 − 旧键，移除 = 旧键 − 新键
        var addedKeys = new HashSet<TKey>(newKeys);
        addedKeys.ExceptWith(_cache.Keys);
        var removedKeys = new HashSet<TKey>(_cache.Keys);
        removedKeys.ExceptWith(newKeys);

        // 创建新增键的缓存对象，完成对象同步
        foreach (var key in addedKeys)
            _cache[key] = _create();

        // 清理已移除条目的缓存对象
        foreach (var key in removedKeys) {
            _remove(_cache[key]);
            _cache.Remove(key);
        }

        // 更新全部缓存对象（在所有增删之后统一更新）
        foreach (var item in source)
            _update(_cache[_getKey(item)], item);
    }
}
