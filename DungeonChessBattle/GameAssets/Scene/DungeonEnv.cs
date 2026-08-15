using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 地牢环境根节点，承载地牢场景的环境表现。
/// 进入战斗后按房间选中的副本键应用对应主题，地面、天空与光照随之切换。
/// </summary>
public partial class DungeonEnv : Node3D {
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
    /// 按副本键应用环境主题。未注册的键保持默认主题。
    /// 战斗开始时由 MainScene 调用。
    /// </summary>
    /// <param name="dungeonKey">房间选中的副本键。</param>
    public void ApplyDungeonTheme(string dungeonKey) {
        switch (dungeonKey) {
            case "deep_cave":
                ApplyTheme(new Color(0.18f, 0.20f, 0.28f, 1f), // 洞窟地面
                    new Color(0.10f, 0.12f, 0.22f, 1f),        // 洞窟天空
                    new Color(0.60f, 0.65f, 1.00f, 1f));       // 青色补光
                break;

            case "goblin_camp":
            default:
                ApplyTheme(new Color(0.28f, 0.38f, 0.24f, 1f), // 林地地面
                    new Color(0.60f, 0.78f, 0.72f, 1f),        // 林地天空
                    new Color(1.00f, 0.95f, 0.85f, 1f));       // 暖白补光
                break;
        }
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
