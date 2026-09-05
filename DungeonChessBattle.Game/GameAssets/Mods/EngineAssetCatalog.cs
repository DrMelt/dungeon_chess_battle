using System;
using System.Collections.Generic;
using Godot;

namespace DungeonChessBattle.Game.GameAssets.Mods;

/// <summary>
/// 引擎预置场景资产目录：mod 的 godot_assets.json 用资产 ID 引用引擎内的
/// 特效/范围提示/环境场景，本类是 ID ↔ res:// 路径的唯一映射。
/// mod 不能携带自绘场景，只引用此目录预置项；未命中 ID 返回 null 由调用方容忍。
/// </summary>
public static class EngineAssetCatalog {
    /// <summary>施放特效场景。</summary>
    public static class ApplyEffects {
        /// <summary>矩形范围伤害施放特效。</summary>
        public const string RectRangeDamage = "apply_effect_rect_range_damage";
    }

    /// <summary>范围提示场景。</summary>
    public static class RangeHints {
        /// <summary>矩形范围提示。</summary>
        public const string Rect = "range_hint_rect";

        /// <summary>圆形区域提示。</summary>
        public const string Circular = "range_hint_circular";
    }

    /// <summary>副本环境场景。</summary>
    public static class EnvScenes {
        /// <summary>默认林地环境。</summary>
        public const string Forest = "env_forest";
    }

    private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal) {
        [ApplyEffects.RectRangeDamage] = "res://GameAssets/Skills/rect_range_damage/effect/effect_skill_rect_range_damage.tscn",
        [RangeHints.Rect] = "res://effects/skill_range/rect/effect_skill_range_rect.tscn",
        [RangeHints.Circular] = "res://effects/skill_range/circular/effect_skill_range_circular.tscn",
        [EnvScenes.Forest] = "res://GameAssets/Dungeon/dungeon_env.tscn",
    };

    /// <summary>按资产 ID 加载 PackedScene；未注册 ID 或资源加载失败返回 null。</summary>
    public static PackedScene? LoadPackedScene(string? assetId) {
        if (string.IsNullOrEmpty(assetId))
            return null;
        if (!Paths.TryGetValue(assetId, out var path))
            return null;
        return GD.Load<PackedScene>(path);
    }
}
