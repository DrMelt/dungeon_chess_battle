using System;
using System.Collections.Generic;
using DungeonChessBattle.Battle.Config.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.Services;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 技能资源强类型映射表（运行时构造 + 类型驱动匹配）。
/// 表不依赖任何 <c>res://</c> 资源文件：内容全部来自 mod，条目由 <c>ModAssetsMapper</c>
/// 以 <see cref="UnitSkillBaseGodot"/> 运行时构造并注册。以 Config（内容注册表中的技能定义实例）
/// 为键构建反查字典，查询不依赖技能键字符串。
/// 表交出资源本体而非副本，条目装配完成后只读。
/// 表实例由 ResourceTables 组合根构造，本类不持有加载入口。
/// </summary>
[GlobalClass]
public partial class SkillResourceTable : Resource {
    /// <summary>运行时查找字典：SkillDefinition → 技能资源本体。</summary>
    private readonly Dictionary<SkillDefinition, UnitSkillBaseGodot> _lookup = [];

    /// <summary>追加运行时 mod 技能资源；同 Config 覆盖已有条目。</summary>
    internal void RegisterModResource(UnitSkillBaseGodot resource) =>
        _lookup[resource.Config] = resource;

    /// <summary>已注册的全部技能资源，均由 <c>ModAssetsMapper</c> 运行时构造。</summary>
    public IReadOnlyCollection<UnitSkillBaseGodot> AllResources => _lookup.Values;

    /// <summary>
    /// 按技能定义取对应的技能资源。
    /// </summary>
    /// <param name="config">内容注册表中的技能定义</param>
    /// <returns>装配期落地的 UnitSkillBaseGodot 本体，同一定义恒取同一实例，调用方只读</returns>
    /// <exception cref="KeyNotFoundException">
    /// 定义未装配到客户端技能资源表时抛出。
    /// </exception>
    public UnitSkillBaseGodot LoadResource(SkillDefinition config) {
        if (_lookup.TryGetValue(config, out var resource))
            return resource;

        throw new KeyNotFoundException(
            $"SkillDefinition '{config.SkillId.Id}' 未装配到客户端技能资源表。" +
            " 请检查该技能的展示数据是否已由对应 mod 注册。");
    }

    /// <summary>
    /// 按技能强类型 ID 取对应的技能资源。
    /// </summary>
    /// <param name="skillKey">技能配置键。</param>
    /// <returns>技能资源本体；技能未注册或无展示条目时返回 null。</returns>
    public UnitSkillBaseGodot? GetResourceBySkillId(SkillKeyId skillKey) {
        var config = ServiceLocator.GameContent.Registry.GetSkill(skillKey);
        if (config == null)
            return null;
        try {
            return LoadResource(config);
        }
        catch (KeyNotFoundException) {
            return null;
        }
    }

    /// <summary>
    /// 自检：验证内容中全部单位引用的技能都在资源表注册。
    /// 客户端启动时调用一次，未注册技能启动即报错而非进副本后崩溃。
    /// </summary>
    public void Validate() {
        foreach (var unit in ServiceLocator.GameContent.Units.All) {
            foreach (var skill in unit.Skills) {
                if (_lookup.ContainsKey(skill))
                    continue;
                throw new InvalidOperationException(
                    $"自检失败：单位 '{unit.ConfigKey}' 引用的技能 SkillId={skill.SkillId.Id} " +
                    "未装配到客户端技能资源表。");
            }
        }
    }
}
