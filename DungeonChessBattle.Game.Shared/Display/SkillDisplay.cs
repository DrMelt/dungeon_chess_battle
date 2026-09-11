using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// 技能展示数据。
/// </summary>
/// <param name="Id">技能键，与内容注册表里的技能身份对齐。</param>
/// <param name="Name">技能名称。</param>
/// <param name="Description">技能描述。</param>
/// <param name="Icon">技能图标，未配置或解析失败为 null。</param>
/// <param name="RangeHintScene">选位置目标时的范围提示场景模板，未配置为 null。</param>
public sealed record SkillDisplay(
    string Id,
    string Name,
    string Description,
    Texture2D? Icon,
    PackedScene? RangeHintScene);
