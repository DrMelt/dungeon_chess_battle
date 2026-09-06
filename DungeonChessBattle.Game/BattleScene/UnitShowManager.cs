using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.Mod;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.BattleScene;

/// <summary>
/// 单位展示管理器：单位视图（UnitGameShow）的全生命周期所有者。
/// 直持统一数据源 <see cref="BattleSessionContext"/> 每帧对齐驱动，在线与回放各随其组装场景持有一份。
/// 单位数据查询与玩家操作归 BattleSessionContext，本组件无绑定生命周期：
/// 收不到进出通知，按数据源绑定代次自检换向并清场旧视图。
/// </summary>
public partial class UnitShowManager : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitShowManager> _logger = ServiceLocator.GetLogger<UnitShowManager>();

    /// <summary>单位展示场景（unit_game_show.tscn）。</summary>
    [Export]
    private PackedScene? _unitShowScene;

    /// <summary>统一数据源引用，在线与回放均由 BattleSessionContext 投影。</summary>
    [Export]
    private BattleSessionContext? _sessionRef;

    /// <summary>单位网络实体 ID → UnitGameShow 映射。</summary>
    private readonly Dictionary<ushort, UnitGameShow> _unitShows = [];

    /// <summary>上次对齐的绑定代次，与数据源当前值不等即视为换向。</summary>
    private long _bindGeneration;

    /// <summary>节点就绪：校验导出引用。</summary>
    public override void _Ready() {
        if (_sessionRef == null)
            _logger.LogError("_sessionRef is not assigned!");
    }

    /// <summary>
    /// 每帧对齐统一数据源：绑定代次变化先清场，再按 netId 重取单位视图引用、按死亡状态收敛可见性，对新增单位生成视图。
    /// 在线单位晚到与回放 Seek 重建均在此收敛；死亡不经事件通报，视图只隐藏不销毁。
    /// </summary>
    public override void _Process(double delta) {
        var source = _sessionRef;
        if (source == null)
            return;

        // 常驻节点收不到进出通知：换绑与解绑由绑定代次自检，未绑定态取数恒空故清场后自然空转
        if (source.BindGeneration != _bindGeneration) {
            _bindGeneration = source.BindGeneration;
            ClearUnits();
        }

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
        // 单位展示经展示索引取：视图未注册时保持内置共享模板原样（ModelScene/BodyColor 均空）
        unitShow.ApplyUnitDisplay(ModAssets.Unit(unit.UnitName));
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
