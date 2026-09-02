using System;
using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 技能资源强类型映射表（基于 .tres 资源文件 + 类型驱动匹配）。
///
/// 在 Godot 编辑器中通过 [Export] 拖拽所有技能 .tres 资源到 SkillResources 数组。
/// 运行时通过每个资源的 Config 属性（返回 GameConfigDB 中的唯一静态技能定义实例）
/// 自动构建反向查找字典，以技能定义对象为键，查询不依赖技能键字符串。
///
/// 新增技能时只需在 res_skill_resource_table.tres 中拖入对应的 .tres 资源即可。
/// 表实例由 ResourceTables 组合根加载并调用 Initialize，本类不持有加载入口。
/// </summary>
[GlobalClass]
public partial class SkillResourceTable : Resource {
    /// <summary>在 Godot 编辑器中拖拽的全部技能资源。</summary>
    [Export]
    public Godot.Collections.Array<UnitSkillBaseGodot> SkillResources { get; set; } = [];

    /// <summary>运行时查找字典：SkillDefinition → 技能资源副本。</summary>
    private readonly Dictionary<SkillDefinition, UnitSkillBaseGodot> _lookup = [];
    private bool _initialized;

    /// <summary>
    /// 初始化查找字典。每个技能资源的 Config 属性返回 GameConfigDB 中的
    /// 唯一静态技能定义实例，因此可以用 Config 作为 Key 精准匹配。
    /// 由 ResourceTables 加载后调用，幂等。
    /// </summary>
    internal void Initialize() {
        if (_initialized)
            return;

        foreach (var res in SkillResources) {
            // 直接以原始资源为模板（只读访问 Config，不修改原始资源）
            var config = res.InternalConfig;
            if (config != null) {
                _lookup[config] = res;
            }
        }

        _initialized = true;
    }

    /// <summary>
    /// 通过技能定义查找并创建对应的 Godot 技能资源实例。
    /// </summary>
    /// <param name="config">GameConfigDB 中的技能定义</param>
    /// <returns>UnitSkillBaseGodot 子类的新副本</returns>
    /// <exception cref="KeyNotFoundException">
    /// 定义未在资源表 .tres 中注册时抛出。
    /// </exception>
    public UnitSkillBaseGodot LoadResource(SkillDefinition config) {
        if (_lookup.TryGetValue(config, out var template))
            return (UnitSkillBaseGodot)template.Duplicate();

        throw new KeyNotFoundException(
            $"SkillDefinition '{config.GetType().Name}' 未在 res_skill_resource_table.tres 中注册。" +
            " 请在 Godot 编辑器中打开该文件，将对应的技能 .tres 资源拖入 SkillResources 数组。");
    }

    /// <summary>
    /// 通过技能强类型 ID 查找并创建对应的 Godot 技能资源实例。
    /// </summary>
    /// <param name="skillKey">技能配置键。</param>
    /// <returns>UnitSkillBaseGodot 子类的新副本；未找到返回 null。</returns>
    public UnitSkillBaseGodot? GetResourceBySkillId(SkillKeyId skillKey) {
        var config = GameConfigDB.GetSkillById(skillKey);
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
    /// 自检：验证 UnitRegistry 全部单位引用的技能都在资源表注册。
    /// 客户端启动时调用一次，未注册技能启动即报错而非进副本后崩溃。
    /// </summary>
    public void Validate() {
        foreach (var unit in UnitRegistry.Instance.All) {
            foreach (var skill in unit.Skills) {
                if (_lookup.ContainsKey(skill))
                    continue;
                throw new InvalidOperationException(
                    $"自检失败：单位 '{unit.ConfigKey}' 引用的技能 SkillId={skill.SkillId.Id} " +
                    $"({skill.GetType().Name}) 未在 res_skill_resource_table.tres 的 SkillResources 中注册。");
            }
        }
    }
}
