using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Game.MainScene.scenes;
using Godot;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 战斗会话上下文（聚合根）：把会话只读投影（<see cref="BattleSessionState"/>）与玩家命令
/// （<see cref="IBattleSessionCommand"/>）聚合为 UI/相机的统一入口，并承载会话生命周期与
/// Running 阶段响应。只读数据经投影取，命令经窄契约下发，均不向外暴露网络对象与服务细节。
/// 由 MainScene 进出战斗时 Bind/Unbind；单位视图（UnitGameShow）生命周期归 UnitShowManager。
/// </summary>
public partial class BattleSessionContext : Node {
    /// <summary>只读会话投影。</summary>
    private readonly BattleSessionState _state = new();

    /// <summary>玩家命令窄契约实现。</summary>
    private readonly BattleSessionCommand _command = new();

    /// <summary>玩家命令窄契约（供预输入缓冲直接消费，UI 不接触服务）。</summary>
    public IBattleSessionCommand Command => _command;

    // =============================================================
    // 只读投影（委托 BattleSessionState）
    // =============================================================

    /// <summary>场景全部单位展示视图集合，由在线战斗世界提供（UI 展示数据源）。</summary>
    public IReadOnlyList<IUnitUiView> Units => _state.Units;

    /// <summary>按网络 ID 查询单位展示视图，不存在返回 null。</summary>
    public IUnitUiView? FindUnit(ushort netId) => _state.FindUnit(netId);

    /// <summary>本地玩家单位的展示视图，控制器未就绪时返回 null。</summary>
    public IUnitUiView? LocalUnit => _state.LocalUnit;

    /// <summary>本地玩家单位的聚焦目标展示视图；焦点为 0 或无目标时返回 null。</summary>
    public IUnitUiView? LocalFocus => _state.LocalFocus;

    /// <summary>本地玩家单位的施法判定视图（权威位置），控制器未就绪时返回 null。</summary>
    public ISkillCasterView? LocalCaster => _state.LocalCaster;

    /// <summary>按网络 ID 查询施法判定视图（权威位置），不存在返回 null。</summary>
    public ISkillCasterView? FindCaster(ushort netId) => _state.FindCaster(netId);

    /// <summary>当前房间副本键，来自服务端权威 BattleRoomEntity 同步；实体未同步时为 null。</summary>
    public string? DungeonKey => _state.DungeonKey;

    /// <summary>战斗开始时刻（服务端权威 Unix 秒），未进战斗或实体未同步时为 null。</summary>
    public long? BattleStartUnixTime => _state.BattleStartUnixTime;

    /// <summary>当前房间会话事件日志的只读视图。</summary>
    public IReadOnlyList<BattleEventLogEntry> EventLog => _state.EventLog;

    /// <summary>当前房间会话事件日志版本号，会话重置时自增。</summary>
    public long EventLogVersion => _state.EventLogVersion;

    /// <summary>是否已在战斗中（会话已绑定）。</summary>
    public bool IsInBattle => _state.IsInBattle;

    /// <summary>获取阵营关系函数用于领域判定（技能预拦等）；未就绪返回 false。</summary>
    public bool TryGetCampRelations(out CampRelationResolver relations) => _state.TryGetCampRelations(out relations);

    /// <summary>解析目标阵营列表相对本地玩家的关系；本地单位或关系函数未就绪返回 Unknown。</summary>
    public CampRelation ResolveLocalCampRelation(IReadOnlyList<string> targetCamps) => _state.ResolveLocalCampRelation(targetCamps);

    // =============================================================
    // 玩家命令（委托 BattleSessionCommand）
    // =============================================================

    /// <summary>请求本地玩家单位设置聚焦目标，0 表示清除。</summary>
    public void SetLocalFocusTarget(ushort targetNetId) => _command.SetLocalFocusTarget(targetNetId);

    // =============================================================
    // 生命周期
    // =============================================================

    /// <summary>进入战斗：注入房间客户端并装配只读投影与命令。</summary>
    public void Bind(RoomBattleClient roomClient, string roomId) {
        _state.Bind(roomClient);
        _command.Bind(roomClient, roomId);
    }

    /// <summary>退出战斗：释放全部会话引用。</summary>
    public void Unbind() {
        _state.Unbind();
        _command.Unbind();
    }

    /// <summary>节点退出场景树：兜底释放（防止战斗中途场景被释放导致引用悬挂）。</summary>
    public override void _ExitTree() {
        Unbind();
    }

    /// <summary>战斗阶段 Running 的会话侧响应：就绪校验等会话业务在此收敛。</summary>
    public void OnBattleRunning() {
        _state.AssertCampRelationsReady();
    }
}
