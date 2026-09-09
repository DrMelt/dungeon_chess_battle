using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// Buff 展示数据：Buff 的领域侧身份只有同步数值 ID，故查询键即 <c>BuffTypeId</c>。
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
