using System;
using System.Collections.Generic;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Game.Mod;
using DungeonChessBattle.Game.Shared;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 内置展示数据注册器：把引擎预置场景与三张资源表的条目视图注册进展示注册表。
/// 必须在 mod 侧注册之前调用，同键条目才被 mod 覆盖。
/// 可被 <c>.tres</c>/<c>.tscn</c> 引用的资源类与 <c>res://</c> 路径只能留在本工程，故这一步不由 <c>Game.Mod</c> 承担。
/// </summary>
public static class BuiltinDisplayAssets {
    /// <summary>引擎预置场景的资源名，mod 展示代码以此引用特效、范围提示与环境场景。</summary>
    public static class AssetIds {
        /// <summary>矩形范围伤害施放特效。</summary>
        public const string RectRangeDamage = "apply_effect_rect_range_damage";

        /// <summary>矩形范围提示。</summary>
        public const string RangeHintRect = "range_hint_rect";

        /// <summary>圆形区域提示。</summary>
        public const string RangeHintCircular = "range_hint_circular";

        /// <summary>默认林地环境。</summary>
        public const string EnvForest = "env_forest";
    }

    /// <summary>引擎预置资源名 ↔ res:// 路径。mod 只能引用此处登记的对象，不能自带脚本类场景节点。</summary>
    private static readonly Dictionary<string, string> EngineScenePaths = new(StringComparer.Ordinal) {
        [AssetIds.RectRangeDamage] = "res://GameAssets/Skills/rect_range_damage/effect/effect_skill_rect_range_damage.tscn",
        [AssetIds.RangeHintRect] = "res://effects/skill_range/rect/effect_skill_range_rect.tscn",
        [AssetIds.RangeHintCircular] = "res://effects/skill_range/circular/effect_skill_range_circular.tscn",
        [AssetIds.EnvForest] = "res://GameAssets/Dungeon/dungeon_env.tscn",
    };

    /// <summary>注册内置资源名与三张表的全部条目视图。须在内容装配完成、资源表可初始化之后调用。</summary>
    public static void Register(IModDisplayRuntime runtime) {
        foreach (var (name, path) in EngineScenePaths) {
            string scenePath = path;
            runtime.RegisterScene(name, () => GD.Load<PackedScene>(scenePath));
        }

        // 资源表在 mod 注册前已是纯内置条目，注册进来的即内置展示真相
        foreach (var skill in ResourceTables.Skills.AllResources)
            runtime.RegisterSkill(skill);
        foreach (var buff in ResourceTables.Buffs.AllResources)
            runtime.RegisterBuff(buff);
        foreach (var dungeon in ResourceTables.Dungeons.AllResources)
            runtime.RegisterDungeon(dungeon);

        // 单位没有编辑器资源表，内置单位外观即共享模板回退；注册空占位让索引查到内容单位的合并起点，
        // mod 只改模型/配色不声明显示名时仍保配置键名，与其余三表「内容有展示无即补占位」同形状
        foreach (var unit in GameContentHost.Registry.Units)
            runtime.RegisterUnit(new BuiltInUnitView(unit.ConfigKey));
    }

    /// <summary>内置单位展示占位视图：仅承载显示名回退配置键，模型/配色留空由消费方回落共享模板。</summary>
    private sealed class BuiltInUnitView(string configKey) : IUnitView {
        /// <inheritdoc/>
        public string ConfigKey => configKey;

        /// <inheritdoc/>
        public string DisplayName => configKey;

        /// <inheritdoc/>
        public string Description => "";

        /// <inheritdoc/>
        public Texture2D? Icon => null;

        /// <inheritdoc/>
        public PackedScene? ModelScene => null;

        /// <inheritdoc/>
        public Color? BodyColor => null;
    }
}
