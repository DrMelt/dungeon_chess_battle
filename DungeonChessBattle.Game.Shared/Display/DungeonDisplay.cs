using DungeonChessBattle.Battle.Shared.ValueObjects;
using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// 副本展示数据。
/// </summary>
/// <param name="DungeonKey">副本键，与内容注册表里的副本身份同一；键长上限由 <see cref="DungeonKeyId"/> 承担。</param>
/// <param name="DisplayName">副本显示名。</param>
/// <param name="Description">副本描述。</param>
/// <param name="EnvScene">环境表现场景模板，主题已在场景内固化，未配置为 null，消费方不建环境。</param>
public sealed record DungeonDisplay(
    DungeonKeyId DungeonKey,
    string DisplayName,
    string Description,
    PackedScene? EnvScene);
