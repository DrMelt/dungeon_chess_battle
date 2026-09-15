using DungeonChessBattle.Battle.Shared.Combat;
using Godot;

namespace DungeonChessBattle.Game.Shared.Display;

/// <summary>
/// 技能展示数据：字段为空串或 null 即未声明，名称经 <see cref="DisplayLabel"/> 回退技能键，其余回退由消费方决定。
/// </summary>
/// <param name="SkillId">技能键，与内容注册表、同步载荷同一身份；键长上限由 <see cref="SkillKeyId"/> 承担。</param>
/// <param name="Name">技能名称。</param>
/// <param name="Description">技能描述。</param>
/// <param name="Icon">技能图标，未配置或解析失败为 null。</param>
/// <param name="RangeHintScene">选位置目标时的范围提示场景模板，未配置为 null。</param>
public sealed record SkillDisplay(
    SkillKeyId SkillId,
    string Name,
    string Description,
    Texture2D? Icon,
    PackedScene? RangeHintScene) {
    /// <summary>展示用名称：名称未声明时取技能键。</summary>
    public string DisplayLabel => string.IsNullOrEmpty(Name) ? SkillId.Id : Name;
}
