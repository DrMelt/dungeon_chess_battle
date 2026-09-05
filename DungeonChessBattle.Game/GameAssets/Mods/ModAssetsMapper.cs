using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.Battle.Mod;
using DungeonChessBattle.Game.Mod;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GameAssets.Mods;

/// <summary>
/// mod 展示资源装配：把各 mod 的 godot_assets.json + images 目录
/// 构造成运行时展示资源（ModSkillResource / ModBuffResource / ModDungeonResource），注册进三张资源表。
/// 必须在内容装配（GameContentHost 重建注册表）之后、任何资源表消费之前调用一次。
/// </summary>
public static class ModAssetsMapper {
    private static readonly ILogger Logger = ServiceLocator.CreateLogger(nameof(ModAssetsMapper));

    /// <summary>
    /// 装配全部 mod 展示资源；无资源数据的 mod 自动跳过。
    /// 图标从 images 目录运行时加载，引用引擎预置场景经 <see cref="EngineAssetCatalog"/> 解析。
    /// </summary>
    public static void Apply(ContentSetRegistry registry, IReadOnlyList<LoadedMod> mods) {
        // 先触发三张表加载并 Initialize，RegisterModResource 要求表已初始化
        var skills = ResourceTables.Skills;
        var buffs = ResourceTables.Buffs;
        var dungeons = ResourceTables.Dungeons;

        foreach (var mod in mods) {
            ModAssetsPackage? package;
            try {
                package = ModAssetsLoader.Load(mod.DirectoryPath);
            }
            catch (InvalidOperationException ex) {
                // 展示数据解析失败只影响该 mod 的展示装配，不拖累数据面与其他 mod
                Logger.LogWarning("mod {ModId} 展示资源装配失败，跳过: {Message}", mod.Manifest.Id, ex.Message);
                continue;
            }
            if (package is null)
                continue;

            var assets = package.Assets;
            string? imagesRootPath = package.ImagesRootPath;

            foreach (var entry in assets.Skills) {
                var config = registry.GetSkill(new SkillKeyId(entry.Id));
                if (config is null)
                    continue;
                var resource = new ModSkillResource(config);
                resource.ApplyViewData(
                    LoadIcon(imagesRootPath, entry.Icon),
                    entry.Name,
                    entry.Description,
                    EngineAssetCatalog.LoadPackedScene(entry.ApplyEffect),
                    EngineAssetCatalog.LoadPackedScene(entry.RangeHint));
                skills.RegisterModResource(resource);
            }

            foreach (var entry in assets.Buffs) {
                var config = registry.GetBuffByKey(entry.Id);
                if (config is null)
                    continue;
                var resource = new ModBuffResource(config);
                resource.ApplyViewData(LoadIcon(imagesRootPath, entry.Icon), entry.Name, entry.Description);
                buffs.RegisterModResource(resource);
            }

            foreach (var entry in assets.Dungeons) {
                var config = registry.GetDungeon(entry.Key);
                if (config is null)
                    continue;
                var resource = new ModDungeonResource(config);
                resource.ApplyViewData(
                    ParseColor(entry.GroundColor, new Color(0.28f, 0.38f, 0.24f, 1f)),
                    ParseColor(entry.SkyColor, new Color(0.60f, 0.78f, 0.72f, 1f)),
                    ParseColor(entry.LightColor, new Color(1.00f, 0.95f, 0.85f, 1f)),
                    EngineAssetCatalog.LoadPackedScene(entry.EnvScene),
                    entry.DisplayName,
                    entry.Description);
                dungeons.RegisterModResource(resource);
            }
        }
    }

    /// <summary>从 mod images 目录加载图标；未配置或文件缺失返回 null。</summary>
    private static ImageTexture? LoadIcon(string? imagesRootPath, string? iconName) {
        if (string.IsNullOrEmpty(iconName) || imagesRootPath is null)
            return null;
        string path = System.IO.Path.Combine(imagesRootPath, iconName);
        if (!System.IO.File.Exists(path))
            return null;
        var image = Image.LoadFromFile(path);
        return image is null ? null : ImageTexture.CreateFromImage(image);
    }

    /// <summary>解析 CSS 十六进制颜色（#RRGGBB / #RRGGBBAA）；解析失败回退默认值。</summary>
    private static Color ParseColor(string? text, Color fallback) {
        if (string.IsNullOrEmpty(text))
            return fallback;
        string hex = text.TrimStart('#');
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            return fallback;
        return new Color(text);
    }
}
