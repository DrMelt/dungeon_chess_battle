using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 单位展示管理器：单位视图（UnitGameShow）的全生命周期所有者。
/// 面向 IBattleViewSource 每帧对齐驱动，在线经在线战斗世界、回放经 ReplayEngine。
/// 单位数据查询与玩家操作归 BattleSessionContext。
/// 由 MainScene / 回放控制器在进入/退出战斗时 Bind/Unbind。
/// </summary>
public partial class UnitShowManager : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitShowManager> _logger = ServiceLocator.GetLogger<UnitShowManager>();

    /// <summary>单位展示场景（unit_game_show.tscn）。</summary>
    [Export]
    private PackedScene? _unitShowScene;

    /// <summary>当前展示数据源（Bind 时注入），在线为在线战斗世界、回放为 ReplayEngine。</summary>
    private IBattleViewSource? _source;

    /// <summary>单位网络实体 ID → UnitGameShow 映射。</summary>
    private readonly Dictionary<ushort, UnitGameShow> _unitShows = [];

    /// <summary>进入战斗：注入展示数据源并清空旧视图。</summary>
    public void Bind(IBattleViewSource source) {
        ClearUnits();
        _source = source;
    }

    /// <summary>退出战斗：清理全部单位视图并释放数据源。</summary>
    public void Unbind() {
        ClearUnits();
        _source = null;
    }

    /// <summary>节点退出场景树：兜底退订（防止战斗中途场景被释放导致事件悬挂）。</summary>
    public override void _ExitTree() {
        Unbind();
    }

    /// <summary>
    /// 每帧对齐展示数据源：按 netId 重取单位视图引用、按死亡状态收敛可见性，对新增单位生成视图。
    /// 在线单位晚到与回放 Seek 重建均在此收敛；死亡不经事件通报，视图只隐藏不销毁。
    /// </summary>
    public override void _Process(double delta) {
        var source = _source;
        if (source == null)
            return;

        foreach (var (netId, show) in _unitShows) {
            var unit = source.FindUnit(netId);
            if (unit == null) {
                show.Visible = false;
                continue;
            }
            show.Unit = unit;
            show.Visible = !unit.IsDead;
        }

        foreach (var unit in source.Units) {
            if (!_unitShows.ContainsKey(unit.UnitId))
                SpawnUnit(unit);
        }
    }

    /// <summary>实例化单位视图并登记；可见性不在此决定，由首帧对齐按死亡状态收敛。</summary>
    private void SpawnUnit(IUnitUiView unit) {
        string unitName = unit.UnitName;

        if (_unitShowScene == null)
            return;
        var unitShow = _unitShowScene.Instantiate<UnitGameShow>();
        if (unitShow == null)
            return;

        // 注入本地展示视图（setter 先于挂载，_Ready 校验不会误报）
        unitShow.Unit = unit;

        AddChild(unitShow);
        _unitShows[unit.UnitId] = unitShow;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Spawned unit '{UnitName}' at {Position}", unitName, unit.Position);
    }

    private void ClearUnits() {
        foreach (var (_, unitShow) in _unitShows) {
            unitShow.QueueFree();
        }
        _unitShows.Clear();
    }
}
