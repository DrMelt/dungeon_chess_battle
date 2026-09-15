using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.BattleScene;

/// <summary>
/// 副本环境管理器：battle_world 常驻组件，令自身子节点里的副本环境实例与统一数据源的权威副本键保持一致。
/// 在线与回放共用本组件，直持 <see cref="BattleSessionContext"/> 取数、不认识会话与回放引擎类型，
/// 数据源换向按 <see cref="BattleSessionContext.BindGeneration"/> 自检，与 UnitShowManager 同口径。
/// 主题烘焙在环境场景模板内，副本键变化只能整棵重建；无键即无环境，有键无资源响亮告警不静默。
/// 环境为纯表现节点，不参与拾取与结算。
/// </summary>
public partial class DungeonEnvironmentManager : Node3D {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<DungeonEnvironmentManager> _logger =
        ServiceLocator.GetLogger<DungeonEnvironmentManager>();

    /// <summary>统一数据源引用，副本键的唯一来源。</summary>
    [Export]
    private BattleSessionContext? _sessionRef;

    /// <summary>当前环境实例，无环境时为 null。</summary>
    private Node3D? _dungeonEnv;

    /// <summary>环境实例对应的副本键，无环境时为 null。</summary>
    private string? _dungeonEnvKey;

    /// <summary>上次对齐的绑定代次，与数据源当前值不等即视为换向。</summary>
    private long _bindGeneration;

    /// <summary>节点就绪：校验导出引用。</summary>
    public override void _Ready() {
        if (_sessionRef == null)
            _logger.LogError("_sessionRef is not assigned!");
    }

    /// <summary>
    /// 每帧对齐：绑定代次或副本键变化即整棵重建环境，稳态只比一次代次与一次键引用。
    /// 权威键晚于绑定到达、换绑到另一副本、解绑回空态都在此收敛。
    /// </summary>
    public override void _Process(double delta) {
        var session = _sessionRef;
        if (session == null)
            return;

        string? key = session.DungeonKey;
        if (session.BindGeneration == _bindGeneration && key == _dungeonEnvKey)
            return;

        _bindGeneration = session.BindGeneration;
        RebuildEnvironment(key);
    }

    /// <summary>整棵换掉环境实例：先销毁旧实例，再按新键装配；键在实例化前先落定，避免缺资源时逐帧重试刷屏。</summary>
    private void RebuildEnvironment(string? key) {
        if (_dungeonEnv != null) {
            RemoveChild(_dungeonEnv);
            _dungeonEnv.QueueFree();
            _dungeonEnv = null;
        }
        _dungeonEnvKey = key;

        if (string.IsNullOrWhiteSpace(key))
            return;

        if (ServiceLocator.ModAssets?.Dungeon(key)?.EnvScene?.Instantiate<Node3D>() is not { } env) {
            _logger.LogWarning("副本 '{DungeonKey}' 无环境场景资源，战场缺少地面与光照。", key);
            return;
        }

        AddChild(env);
        _dungeonEnv = env;
    }
}
