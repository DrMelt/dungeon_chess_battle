using DungeonChessBattle.Battle.GameConfig;
using Godot;
using DungeonChessBattle.Game.GameAssets.Mods;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// Godot 端 mod 装配管理器：懒装配 user://mods 内容到全局注册表、行为目录与展示资源表。
/// 主场景 _Ready 首个调用，保证任何 UI/资源表访问前内容已就绪；
/// 服务器子进程由 ServerProcessHost 注入同一根目录，两端装配同源。
/// </summary>
public static class ModManager {
    /// <summary>mods 根目录绝对路径。</summary>
    public static string ModsRootPath => ProjectSettings.GlobalizePath("user://mods");

    private static ContentBootResult? _boot;

    /// <summary>最近一次装配结果；未装配为 null。</summary>
    public static ContentBootResult? Boot => _boot;

    /// <summary>执行一次装配，幂等；失败不抛，错误经 <see cref="ContentBootResult.Errors"/> 供 UI 展示。</summary>
    public static void EnsureInitialized() {
        if (_boot is not null)
            return;
        _boot = ContentBootstrapper.Load(ModsRootPath);

        // 逻辑装配完成后立即装配 Godot 展示资源，使资源表与行为目录同源
        ModAssetsMapper.Apply(GameContentHost.Registry, _boot.Mods);
    }
}
