using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// 展示数据：注册器收发的条目形状，内置资源类与 mod 展示代码各自产出。
/// 未声明的字符串成员为空串、资源成员为 null，回退值由消费方决定；
/// 同键后写覆盖时按此语义做字段级合并，见 <c>Game.Mod.Manager</c>。
/// </summary>
/// <param name="Id">技能键，与内容注册表里的技能身份对齐。</param>
/// <param name="Name">技能名称。</param>
/// <param name="Description">技能描述。</param>
/// <param name="Icon">技能图标，未配置或解析失败为 null。</param>
/// <param name="ApplyEffectScene">施放特效场景模板，未配置为 null。</param>
/// <param name="RangeHintScene">选位置目标时的范围提示场景模板，未配置为 null。</param>
public sealed record SkillDisplay(
    string Id,
    string Name,
    string Description,
    Texture2D? Icon,
    PackedScene? ApplyEffectScene,
    PackedScene? RangeHintScene);

/// <summary>
/// Buff 展示数据。Buff 的领域侧身份只有同步数值 ID，故查询键即 BuffTypeId。
/// </summary>
/// <param name="BuffTypeId">跨端同步数值身份，展示查询主键；0 表示未声明，不参与注册。</param>
/// <param name="Name">Buff 名称。</param>
/// <param name="Description">Buff 描述。</param>
/// <param name="Icon">Buff 图标，未配置或解析失败为 null。</param>
public sealed record BuffDisplay(
    ushort BuffTypeId,
    string Name,
    string Description,
    Texture2D? Icon);

/// <summary>
/// 单位展示数据。仅覆盖展示字段，单位数值与行为仍在内容注册表。
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

/// <summary>副本展示数据。</summary>
/// <param name="Key">副本键，与内容注册表里的副本身份对齐。</param>
/// <param name="DisplayName">副本显示名。</param>
/// <param name="Description">副本描述。</param>
/// <param name="EnvScene">环境表现场景模板，主题已在场景内固化，未配置为 null 由消费方回退默认副本场景。</param>
public sealed record DungeonDisplay(
    string Key,
    string DisplayName,
    string Description,
    PackedScene? EnvScene);
