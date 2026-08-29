using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.GamePanels;
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
    /// 每帧对齐展示数据源：按 netId 重取单位视图引用并同步存活，对新增单位生成视图。
    /// 在线单位晚到与回放 Seek 重建均在此收敛。
    /// </summary>
    public void Tick() {
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
            if (!_unitShows.ContainsKey(unit.UnitNetId))
                SpawnUnit(unit);
        }
    }

    private void SpawnUnit(IUnitUiView unit) {
        var unitName = unit.UnitName;

        // 按配置键取配置（技能资源构建来源）
        var config = UnitCatalog.GetByKey(unitName);

        if (_unitShowScene == null)
            return;
        var unitShow = _unitShowScene.Instantiate<UnitGameShow>();
        if (unitShow == null)
            return;

        // 注入本地展示视图（setter 先于挂载，_Ready 校验不会误报）
        unitShow.Unit = unit;

        // 从配置构建 Godot 技能资源列表（展示资源不参与网络同步，两端各自从共享配置读取）
        if (config != null) {
            foreach (var skillDefinition in config.Skills) {
                unitShow.SkillsList.Add(SkillResourceTable.LoadResource(skillDefinition));
            }
        }

        AddChild(unitShow);
        _unitShows[unit.UnitNetId] = unitShow;

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
