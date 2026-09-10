using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// Buff 展示数据：Buff 的领域侧身份即字符串键，故查询键与内容注册表里的 Buff 身份同一。
/// </summary>
/// <param name="BuffTypeId">Buff 键，与内容注册表里的 Buff 身份对齐；空串表示未声明，不参与注册，
/// 长度上限 <c>BuffTypeId.MaxLength</c> 由宿主注册时校验。</param>
/// <param name="Name">Buff 名称。</param>
/// <param name="Description">Buff 描述。</param>
/// <param name="Icon">Buff 图标，未配置或解析失败为 null。</param>
public sealed record BuffDisplay(
    string BuffTypeId,
    string Name,
    string Description,
    Texture2D? Icon);
