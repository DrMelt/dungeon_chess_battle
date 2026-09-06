using DungeonChessBattle.Game.Mod;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 地牢环境场景模板根节点，承载地面、光照与天空表现。
/// 由 DungeonResourceTable.InstantiateEnvironment 按副本键实例化，
/// BattleCoordinator 挂载后调用 ApplyDungeonTheme 装配副本主题，退出战斗即销毁。
/// </summary>
public partial class DungeonEnv : Node3D {
    /// <summary>默认林地主题：地面颜色，与默认副本 goblin_camp（res_dungeon_01）主题同源。</summary>
    private static readonly Color DefaultGroundColor = new(0.28f, 0.38f, 0.24f, 1f);

    /// <summary>默认林地主题：天空背景颜色，与默认副本 goblin_camp（res_dungeon_01）主题同源。</summary>
    private static readonly Color DefaultSkyColor = new(0.60f, 0.78f, 0.72f, 1f);

    /// <summary>默认林地主题：方向光补光颜色，与默认副本 goblin_camp（res_dungeon_01）主题同源。</summary>
    private static readonly Color DefaultLightColor = new(1.00f, 0.95f, 0.85f, 1f);

    /// <summary>地面网格，主题切换时重写材质表面颜色。</summary>
    [Export]
    private MeshInstance3D? _groundMesh;

    /// <summary>世界环境节点，调节天空与背景颜色。</summary>
    [Export]
    private WorldEnvironment? _worldEnv;

    /// <summary>方向光，调节环境补光颜色。</summary>
    [Export]
    private DirectionalLight3D? _sunLight;

    /// <summary>
    /// 按副本键应用环境主题：经展示索引取副本视图的主题参数。
    /// 键为空、副本未注册或视图未声明主题色时逐项回退默认林地主题（与默认副本 goblin_camp 同源）。
    /// 由 BattleCoordinator 在进入战斗与 Running 阶段各调用一次，覆盖实体同步前后的时序差异。
    /// </summary>
    /// <param name="dungeonKey">房间选中的副本键，实体未同步或未注册时为 null。</param>
    public void ApplyDungeonTheme(string? dungeonKey) {
        var view = ModAssets.Dungeon(dungeonKey);
        ApplyTheme(
            view?.GroundColor ?? DefaultGroundColor,
            view?.SkyColor ?? DefaultSkyColor,
            view?.LightColor ?? DefaultLightColor);
    }

    /// <summary>应用主题：地面材质、天空背景与补光颜色。</summary>
    private void ApplyTheme(Color ground, Color sky, Color light) {
        _sunLight?.LightColor = light;

        if (_worldEnv?.Environment != null) {
            _worldEnv.Environment.BackgroundColor = sky;
            _worldEnv.Environment.BackgroundEnergyMultiplier = 1.0f;
        }

        if (_groundMesh?.GetActiveMaterial(0) is StandardMaterial3D material) {
            material = (StandardMaterial3D)material.Duplicate();
            material.AlbedoColor = ground;
            _groundMesh.SetSurfaceOverrideMaterial(0, material);
        }
    }
}
