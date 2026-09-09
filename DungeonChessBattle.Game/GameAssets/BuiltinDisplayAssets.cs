using System;
using System.Collections.Generic;
using System.Linq;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Game.Mod.Shared;
using DungeonChessBattle.Game.Shared.Display;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 引擎侧展示注册器：把引擎预置场景名与内容单位的外观占位注册进展示注册表。
/// 必须在 mod 侧注册之前调用，同键条目才被 mod 覆盖。
/// 引擎不再持有内容条目，技能/Buff/副本三张表此刻仍是空表，条目一律由 mod 声明落地。
/// 可被 <c>.tres</c>/<c>.tscn</c> 引用的 <c>res://</c> 路径只能留在本工程，
/// 故这一步不由 <c>Game.Mod.Manager</c> 承担，由宿主以委托交进装配过程。
/// </summary>
public static class BuiltinDisplayAssets {
    /// <summary>引擎预置资源名 ↔ res:// 路径。资源名定义在 <c>Game.Shared</c> 的 <see cref="DisplayAssetIds"/>，
    /// mod 展示代码以该名引用宿主对象，不必复制一份特效或场景。</summary>
    private static readonly Dictionary<string, string> EngineScenePaths = new(StringComparer.Ordinal) {
        [DisplayAssetIds.RectRangeDamage] = "res://GameAssets/Skills/rect_range_damage/effect/effect_skill_rect_range_damage.tscn",
        [DisplayAssetIds.RangeHintRect] = "res://effects/skill_range/rect/effect_skill_range_rect.tscn",
        [DisplayAssetIds.RangeHintCircular] = "res://effects/skill_range/circular/effect_skill_range_circular.tscn",
        [DisplayAssetIds.EnvForest] = "res://GameAssets/Dungeon/dungeon_env.tscn",
        [DisplayAssetIds.EnvCave] = "res://GameAssets/Dungeon/dungeon_env_cave.tscn",
    };

    /// <summary>注册引擎预置场景名与内容单位的外观占位。须在内容装配完成之后调用。</summary>
    public static void Register(IModDisplayRuntime runtime) {
        foreach (var (name, path) in EngineScenePaths) {
            string scenePath = path;
            runtime.RegisterScene(name, () => GD.Load<PackedScene>(scenePath));
        }

        // 单位没有编辑器资源表，外观即共享模板回退；注册空占位让索引查到内容单位的合并起点，
        // mod 只改模型/配色不声明显示名时仍保配置键名，与其余三表「内容有展示无即补占位」同形状
        foreach (var key in GameContentHost.Registry.Units.Select(unit => unit.ConfigKey))
            runtime.RegisterUnit(new UnitDisplay(key, key, "", null, null, null));
    }
}
