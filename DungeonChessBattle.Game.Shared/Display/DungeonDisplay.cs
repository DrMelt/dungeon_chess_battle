using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// 副本展示数据。
/// </summary>
/// <param name="Key">副本键，与内容注册表里的副本身份对齐。</param>
/// <param name="DisplayName">副本显示名。</param>
/// <param name="Description">副本描述。</param>
/// <param name="EnvScene">环境表现场景模板，主题已在场景内固化，未配置为 null 由消费方回退默认副本场景。</param>
public sealed record DungeonDisplay(
    string Key,
    string DisplayName,
    string Description,
    PackedScene? EnvScene);
