using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 地牢环境根节点，承载地牢场景的环境表现。
/// 进入战斗后按房间选中的副本键经 DungeonResourceTable 装配对应主题，地面、天空与光照随之切换。
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

    /// <summary>副本资源表引用，按副本键装配环境主题。</summary>
    [Export]
    private DungeonResourceTable? _dungeonResourceTable;

    /// <summary>节点就绪：校验导出引用。</summary>
    public override void _Ready() {
        if (_dungeonResourceTable == null)
            GD.PushError("[DungeonEnv] _dungeonResourceTable is not assigned!");
    }

    /// <summary>
    /// 按副本键应用环境主题：经副本资源表取副本资源的主题参数。
    /// 键为空、副本未注册或资源未映射时回退默认林地主题（与默认副本 goblin_camp 同源）。
    /// 由 BattleCoordinator 在进入战斗与 Running 阶段各调用一次，覆盖实体同步前后的时序差异。
    /// </summary>
    /// <param name="dungeonKey">房间选中的副本键，实体未同步或未注册时为 null。</param>
    public void ApplyDungeonTheme(string? dungeonKey) {
        var resource = _dungeonResourceTable?.GetResource(dungeonKey);
        if (resource == null) {
            ApplyTheme(DefaultGroundColor, DefaultSkyColor, DefaultLightColor);
            return;
        }
        ApplyTheme(resource.GroundColor, resource.SkyColor, resource.LightColor);
    }

    /// <summary>恢复编辑器默认主题，退出战斗时调用。</summary>
    public void ResetTheme() {
        _groundMesh?.SetSurfaceOverrideMaterial(0, null);
        _sunLight?.LightColor = new Color(1f, 1f, 1f, 1f);
        if (_worldEnv?.Environment != null) {
            _worldEnv.Environment.BackgroundColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            _worldEnv.Environment.BackgroundEnergyMultiplier = 1.61f;
        }
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
