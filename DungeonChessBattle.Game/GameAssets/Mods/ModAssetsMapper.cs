using System;
using System.Collections.Generic;
using DungeonChessBattle.Battle.Config.Registry;
using DungeonChessBattle.Game.Mod.Manager;
using DungeonChessBattle.Game.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GameAssets.Mods;

/// <summary>
/// mod 展示数据到三张资源表的装配桥：把被 mod 声明过的条目落成运行时资源对象注册进表。
/// 表内已有同键资源时以它为模板复制，只改写 mod 声明了的字段，其余成员原样保留；
/// 表内缺失的领域条目一律补一个运行时资源，保证「内容里有、展示里没有」不再让客户端自检崩。
/// </summary>
/// <remarks>
/// 必须在内容装配（<c>ModManager.EnsureInitialized</c> 建出新注册表）之后调用：表的查找以刚注册的领域定义或其键为身份。
/// 引擎不提供任何条目，故模板只可能是本方法此前已落地的资源。
/// 字段取自展示注册表的合并数据，落地后把资源对象的数据回注注册表，
/// 使「走资源表的渲染」与「走索引的 UI」看到同一份展示真相，缺省名回退也只在资源对象上算一次。
/// </remarks>
public static class ModAssetsMapper {
    private static readonly ILogger Logger = ServiceLocator.CreateLogger(nameof(ModAssetsMapper));

    /// <summary>把 mod 声明过的条目落地成资源对象，并为缺失条目补占位。</summary>
    public static void Apply(ContentSetRegistry registry, ModDeclaration declared, DisplayRegistry display) {
        // 取三张表单例，首次访问时构造空表
        var skills = ResourceTables.Skills;
        var buffs = ResourceTables.Buffs;
        var dungeons = ResourceTables.Dungeons;
        var synthesized = new List<string>();

        foreach (var config in registry.Skills) {
            bool overridden = declared.Skills.Contains(config.SkillId);
            bool hasTemplate = skills.TryGetResource(config, out var template);
            if (!overridden) {
                // mod 无话可说：表里已有条目即完事，没有则补一个只带缺省名的占位资源
                if (!hasTemplate) {
                    var placeholder = new ModSkillResource(config);
                    skills.RegisterModResource(placeholder);
                    display.RegisterSkill(placeholder.ToDisplay());
                    synthesized.Add($"技能 {config.SkillId.Id}");
                }
                continue;
            }

            var data = display.GetSkill(config.SkillId)
                ?? throw new InvalidOperationException($"技能 '{config.SkillId.Id}' 已声明覆盖但展示数据缺失。");
            var resource = hasTemplate && template is { } t
                ? (UnitSkillBaseGodot)t.Duplicate()
                : new ModSkillResource(config);
            resource.ApplyViewData(
                data.Icon, data.Name, data.Description, data.RangeHintScene);
            skills.RegisterModResource(resource);
            display.RegisterSkill(resource.ToDisplay());
        }

        foreach (var config in registry.Buffs) {
            bool overridden = declared.Buffs.Contains(config.BuffTypeId);
            bool hasTemplate = buffs.TryGetResource(config.BuffTypeId, out var template);
            if (!overridden) {
                if (!hasTemplate) {
                    var placeholder = new ModBuffResource(config);
                    buffs.RegisterModResource(placeholder);
                    display.RegisterBuff(placeholder.ToDisplay());
                    synthesized.Add($"Buff {config.BuffTypeId.Value}");
                }
                continue;
            }

            var data = display.GetBuff(config.BuffTypeId)
                ?? throw new InvalidOperationException($"Buff {config.BuffTypeId.Value} 已声明覆盖但展示数据缺失。");
            var resource = hasTemplate && template is { } t
                ? (BuffBaseGodot)t.Duplicate()
                : new ModBuffResource(config);
            resource.ApplyViewData(data.Icon, data.Name, data.Description);
            buffs.RegisterModResource(resource);
            display.RegisterBuff(resource.ToDisplay());
        }

        foreach (var config in registry.Dungeons) {
            bool overridden = declared.Dungeons.Contains(config.DungeonKey);
            bool hasTemplate = dungeons.TryGetResource(config, out var template);
            if (!overridden) {
                if (!hasTemplate) {
                    var placeholder = new ModDungeonResource(config);
                    dungeons.RegisterModResource(placeholder);
                    display.RegisterDungeon(placeholder.ToDisplay());
                    synthesized.Add($"副本 {config.DungeonKey.Value}");
                }
                continue;
            }

            var data = display.GetDungeon(config.DungeonKey)
                ?? throw new InvalidOperationException($"副本 '{config.DungeonKey.Value}' 已声明覆盖但展示数据缺失。");
            var resource = hasTemplate && template is { } t
                ? (DungeonResourceBaseGodot)t.Duplicate()
                : new ModDungeonResource(config);
            resource.ApplyViewData(data.EnvScene, data.DisplayName, data.Description);
            dungeons.RegisterModResource(resource);
            display.RegisterDungeon(resource.ToDisplay());
        }

        if (synthesized.Count > 0 && Logger.IsEnabled(LogLevel.Warning))
            Logger.LogWarning(
                "以下条目缺展示数据，已按占位展示装配：{Entries}", string.Join("、", synthesized));
    }
}
