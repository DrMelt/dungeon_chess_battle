using Godot;
using System;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 地牢配置资源，提供地牢环境场景的实例化入口。
/// </summary>
[GlobalClass]
public partial class DungeonConfig : Resource {
    /// <summary>地牢环境使用的场景资源。</summary>
    [Export]
    private PackedScene? dungeonEnvPKS;

    /// <summary>
    /// 实例化地牢环境节点；未配置场景时抛出异常。
    /// </summary>
    public DungeonEnv DungeonEnvRef {
        get {
            if (dungeonEnvPKS == null)
                throw new InvalidOperationException("[DungeonConfig] [Export] dungeonEnvPKS is not assigned!");
            return dungeonEnvPKS.Instantiate<DungeonEnv>();
        }
    }
}
