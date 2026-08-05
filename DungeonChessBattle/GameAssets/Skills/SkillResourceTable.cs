using System;
using System.Collections.Generic;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle.GameAssets.Skills;

/// <summary>
/// 技能资源强类型映射表（基于 .tres 资源文件 + 类型驱动匹配）。
///
/// 在 Godot 编辑器中通过 [Export] 拖拽所有技能 .tres 资源到 SkillResources 数组。
/// 运行时通过每个资源的 Config 属性（返回 GameConfigDB 中的唯一静态 SkillConfig 实例）
/// 自动构建反向查找字典，无需任何字符串 ID。
///
/// 新增技能时只需在 res_skill_resource_table.tres 中拖入对应的 .tres 资源即可。
/// </summary>
[GlobalClass]
public partial class SkillResourceTable : Resource {
    private static SkillResourceTable Instance {
        get {
            if (field != null)
                return field;

            field = GD.Load<SkillResourceTable>(
                "res://GameAssets/Skills/res_skill_resource_table.tres");
            field.Initialize();
            return field;
        }
    }

    /// <summary>在 Godot 编辑器中拖拽的全部技能资源。</summary>
    [Export]
    public Godot.Collections.Array<UnitSkillBaseGodot> SkillResources { get; set; } = [];

    /// <summary>运行时查找字典：SkillConfig → 技能资源副本。</summary>
    private readonly Dictionary<SkillConfig, UnitSkillBaseGodot> _lookup = [];
    private bool _initialized;

    /// <summary>
    /// 初始化查找字典。每个技能资源的 Config 属性返回 GameConfigDB 中的
    /// 唯一静态 SkillConfig 实例，因此可以用 Config 作为 Key 精准匹配。
    /// </summary>
    private void Initialize() {
        if (_initialized)
            return;

        foreach (var res in SkillResources) {
            // 对每个资源创建一个副本（避免修改原始资源），然后读取其 Config
            var copy = (UnitSkillBaseGodot)res.Duplicate();
            var config = copy.InternalConfig;
            if (config != null) {
                _lookup[config] = copy;
            }
        }

        _initialized = true;
    }

    /// <summary>
    /// 通过 SkillConfig 查找并创建对应的 Godot 技能资源实例。
    /// </summary>
    /// <param name="config">GameConfigDB 中的技能配置</param>
    /// <returns>UnitSkillBaseGodot 子类的新副本</returns>
    /// <exception cref="KeyNotFoundException">
    /// 配置未在资源表 .tres 中注册时抛出。
    /// </exception>
    public static UnitSkillBaseGodot LoadResource(SkillConfig config) {
        var table = Instance; // 触发懒加载

        if (table._lookup.TryGetValue(config, out var template))
            return (UnitSkillBaseGodot)template.Duplicate();

        throw new KeyNotFoundException(
            $"SkillConfig '{config.GetType().Name}' 未在 res_skill_resource_table.tres 中注册。" +
            " 请在 Godot 编辑器中打开该文件，将对应的技能 .tres 资源拖入 SkillResources 数组。");
    }

    /// <summary>
    /// 启动时自检：验证所有 UnitConfig 中引用的技能都在资源表中有注册。
    /// 在游戏启动时调用一次，效果等同于编译期检查。
    /// </summary>
    public static void Validate() {
        var table = Instance;

        // 反射扫描 GameConfigDB 中所有 UnitConfig 静态属性
        var unitConfigType = typeof(GameConfig.GameConfigDB);
        foreach (var prop in unitConfigType.GetProperties(
                     System.Reflection.BindingFlags.Public |
                     System.Reflection.BindingFlags.Static)) {
            if (prop.PropertyType != typeof(UnitConfig))
                continue;

            var unitConfig = (UnitConfig?)prop.GetValue(null)
                ?? throw new InvalidOperationException(
                    $"自检失败：无法获取单位配置 '{prop.Name}' 的值。");
            foreach (var skill in unitConfig.Skills) {
                if (!table._lookup.ContainsKey(skill)) {
                    throw new InvalidOperationException(
                        $"自检失败：单位 '{prop.Name}' 引用的技能 '{skill.GetType().Name}' " +
                        "未在 res_skill_resource_table.tres 的 SkillResources 中注册。");
                }
            }
        }
    }
}
