using DungeonChessBattle.Battle.Shared.ValueObjects;
using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// Buff 展示数据：字段为空串或 null 即未声明，名称经 <see cref="DisplayLabel"/> 回退 Buff 键，其余回退由消费方决定。
/// </summary>
/// <param name="BuffTypeId">Buff 键，与内容注册表、同步载荷同一身份；键长上限由 <see cref="BuffTypeId.MaxLength"/> 承担，
/// 无键的数据不参与注册。</param>
/// <param name="Name">Buff 名称。</param>
/// <param name="Description">Buff 描述。</param>
/// <param name="Icon">Buff 图标，未配置或解析失败为 null。</param>
public sealed record BuffDisplay(
    BuffTypeId BuffTypeId,
    string Name,
    string Description,
    Texture2D? Icon) {
    /// <summary>展示用名称：名称未声明时取 Buff 键。</summary>
    public string DisplayLabel => string.IsNullOrEmpty(Name) ? BuffTypeId.Value : Name;
}
