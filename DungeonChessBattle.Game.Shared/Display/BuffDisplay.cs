using DungeonChessBattle.Battle.Shared.ValueObjects;
using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// Buff 展示数据。
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
    Texture2D? Icon);
