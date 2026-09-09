using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// 单位展示数据：仅覆盖展示字段，单位数值与行为仍在内容注册表。
/// </summary>
/// <param name="ConfigKey">单位配置键，与内容注册表里的单位身份对齐。</param>
/// <param name="DisplayName">单位显示名，未配置时由消费方回退配置键。</param>
/// <param name="Description">单位描述。</param>
/// <param name="Icon">单位图标，未配置或解析失败为 null。</param>
/// <param name="ModelScene">单位模型场景模板，未配置为 null 由消费方回退内置共享模板。</param>
/// <param name="BodyColor">单位主体配色，未配置为 null 由消费方保持模型原样。</param>
public sealed record UnitDisplay(
    string ConfigKey,
    string DisplayName,
    string Description,
    Texture2D? Icon,
    PackedScene? ModelScene,
    Color? BodyColor);
