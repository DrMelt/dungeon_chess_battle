using System.Numerics;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Shared.Inputs;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Entities.SyncData;
using DungeonChessBattle.Replay.Shared;
using DungeonChessBattle.Server.DataStore.Shared;
using LiteEntitySystem;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// BattleRoomServer 的初始化：从 Store 自取数据、单位实体与领域单位对称创建、战斗管理与 RPC 处理。
/// 本 partial 的所有方法仅在房间线程执行。
/// 领域权威在 BattleUnit，UnitPawn 为同步载体；移动由 BattleScene 结算，状态经 UnitPawn.SyncFrom 投影。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>
    /// 房间线程首帧初始化：创建根实体、装配状态同步器、
    /// 从 Store 迁移准备期单位、按副本生成敌人。此后 EntityManager 不再被其他线程触碰。
    /// </summary>
    private void InitializeFromStore() {
        var roomEntity = EntityManager.AddEntity<BattleRoomEntity>(e => {
            e.RoomId.Value = RoomId;
            // 注入服务端权威副本键，客户端据此加载对应的环境场景
            e.DungeonKey.Value = _dungeonKey;
        }) ?? throw new InvalidOperationException($"Failed to create BattleRoomEntity for room '{RoomId}'.");
        _roomEntity = roomEntity;

        // 状态同步器在单位创建后装配，读取已建实体映射；由 BattleLoop 每帧在 Tick 之后显式驱动
        _stateSynchronizer = new BattleStateSynchronizer(this);

        // 从 Store 迁移准备期单位；同阵营按序错开出生点，避免重名/同阵营单位重叠
        var units = _stateStore.GetPrepareUnits(RoomId);
        var playerInfos = new List<ReplayPlayerInfo>(units.Count);
        int campAIndex = 0, campBIndex = 0;
        foreach (var selection in units) {
            // 玩家阵营由副本配置按选项键权威解析；首个阵营为主阵营，作为出生点分边依据
            var camps = ResolvePlayerCamps(selection);
            var spawnPos = camps[0] == CampConstants.CampA
                ? new Vector2(campAIndex++ * SpawnSpacing, 0)
                : new Vector2(5f + campBIndex++ * SpawnSpacing, 0);
            var pawn = CreatePawnEntity(selection.UnitConfigKey, camps, spawnPos);
            _pawnByPlayerId[selection.PlayerId] = pawn;
            // 回放玩家表：下标即记录条目里的玩家序号，敌人与非玩家单位不收录
            playerInfos.Add(new ReplayPlayerInfo(selection.PlayerName, selection.UnitConfigKey, pawn.Id));
        }

        // 按房间选中的副本配置生成敌人，阵营由副本配置统一编队，服务端 AI 驱动
        SpawnDungeonEnemies();

        // 战斗输入回放记录：全部单位创建完成后装配，单位初始态整表落盘——敌人 ID 不再靠
        // 「实体 ID 连续分配」这个前提推演；条目引用了表外单位时门内解析落空，不报错
        CreateReplayRecorder(playerInfos);
        RecordUnitInits();

        // 战斗循环收编进 LES tick 生命周期：Update=输入预备（AI 决策 → 在架施法重试）先于位移，
        // LateUpdate=Tick → 帧末收口（权威状态同步 + 把结束帧写进回放时间轴）→ 整帧事件外送。
        // 帧末收口留在闭包里：_stateSynchronizer 在上方刚赋值，闭包带得走这份可空状态。
        EntityManager.AddLocalSingleton(new BattleLoop(_battleScene, _intentHub,
            scene => {
                _stateSynchronizer.Sync(scene);
                RecordBattleEnd(scene);
            }, HandleBattleFrameEvents));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Initialized from store: {UnitCount} units migrated.",
                RoomId, units.Count);
    }

    /// <summary>
    /// 按副本配置的玩家阵营选项解析选择记录的实际阵营；选项缺失属配置故障，响亮失败。
    /// 仅房间线程调用。
    /// </summary>
    private IReadOnlyList<string> ResolvePlayerCamps(UnitSelection selection) {
        var dungeon = _dungeonRegistry.GetByKey(_dungeonKey);
        var camps = dungeon?.PlayerCampOptions.FirstOrDefault(o => o.Key == selection.CampOptionKey)?.Camps;
        if (camps == null || camps.Count == 0)
            throw new InvalidOperationException(
                $"Room '{RoomId}': camp option '{selection.CampOptionKey}' not found in dungeon '{_dungeonKey}' for unit '{selection.UnitConfigKey}'.");
        return camps;
    }

    /// <summary>
    /// 按房间副本配置生成敌人：UnitPawn 与 BattleUnit 对称创建，敌方在场地对侧按纵队排布。
    /// 仅房间线程调用。
    /// </summary>
    private void SpawnDungeonEnemies() {
        var dungeon = _dungeonRegistry.GetByKey(_dungeonKey);
        if (dungeon == null)
            return;

        foreach (var spawn in dungeon.Enemies) {
            // 敌人生成以注册表权威配置键为准，杜绝错配
            var config = _unitRegistry.GetByConfig(spawn.Unit)
                ?? throw new InvalidOperationException(
                    $"Dungeon '{_dungeonKey}' references unregistered unit config for enemy spawn.");
            for (int i = 0; i < spawn.Count; i++) {
                var spawnPos = new Vector2(spawn.SpawnBaseX + i * spawn.SpawnXSpacing, 0);
                CreatePawnEntity(config.ConfigKey, dungeon.EnemyCamps, spawnPos);
            }
        }
    }

    /// <summary>
    /// 在本房间的 SEM 中创建 UnitPawn 实体，并按同一 NetId 创建领域单位 BattleUnit 注册进战斗世界。
    /// 战斗系数与技能装配在 BattleUnit，状态同步器写 SyncVar 供客户端展示。仅房间线程调用。
    /// </summary>
    public UnitPawn CreatePawnEntity(UnitConfigKey unitName, IReadOnlyList<string> camps, Vector2 spawnPos) {
        if (!CampConstants.IsValidCamps(camps))
            throw new InvalidOperationException(
                $"Invalid camps '{(camps == null ? string.Empty : string.Join(",", camps))}' for unit '{unitName}' in room '{RoomId}'.");

        var entity = EntityManager.AddEntity<UnitPawn>(e => {
            e.UnitKeyName.Value = unitName;
            var campsData = new SyncCampsData();
            campsData.Set(camps);
            e.CampsData.Value = campsData;
            e.Position.Value = spawnPos;
        }) ?? throw new InvalidOperationException($"Failed to create UnitPawn for unit '{unitName}' in room '{RoomId}'.");

        // 订阅该 Pawn 的玩家输入回调；技能/聚焦请求改经 UnitController 可靠通道进入
        entity.InputHandler = OnPawnInput;
        _roomPawns.Add(entity);
        _pawnByNetId[entity.Id] = entity;

        // 领域单位（权威）：战斗世界结算读写，状态同步器写 SyncVar
        var config = _unitRegistry.GetByKey(unitName)
            ?? throw new InvalidOperationException($"Unknown unit config key '{unitName}' in room '{RoomId}'.");
        var unit = BattleUnitFactory.Create(config, entity.Id, camps, spawnPos);
        _battleScene.AddUnit(unit);

        // 技能定义供客户端 UnitGameShow 装配展示资源，本地字段不参与网络同步
        entity.Skills = config.Skills;

        return entity;
    }

    /// <summary>在本房间启动战斗：把战斗世界阶段置为 Running，阶段经状态同步器投影到房间载体，起始 tick 写入回放记录。</summary>
    public void StartBattle() {
        _battleScene.CurrentPhase = BattlePhase.Running;
        _replayRecorder?.SetStartTick(EntityManager.Tick);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Battle started, phase={Phase}", RoomId, _battleScene.CurrentPhase);
    }

    /// <summary>处理经 UnitPawn 实例事件到达的玩家移动输入：转成玩家命令交输入门面，位移由领域 <c>BattleScene.Tick</c> 结算。</summary>
    private void OnPawnInput(UnitPawn pawn, UnitInputPacket input, float deltaTime) {
        SubmitAndRecord(PlayerCommand.Move(pawn.Id, input.MoveX, input.MoveY));

        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("[RoomId: {RoomId}] PawnInput: {Unit} dir={Dir}, dt={Dt}",
                RoomId, pawn.UnitKeyName.Value, input.MoveDirection, deltaTime);
    }

    /// <summary>
    /// 整帧领域事件日志整帧编码经可靠通道外送，空帧不发。
    /// 单位与房间级状态已由 BattleStateSynchronizer 在 Tick 后写 SyncVar；死亡不走事件，
    /// 由生命值下行派生，断线重连后随状态自愈。仅房间线程调用。
    /// </summary>
    private void HandleBattleFrameEvents(IReadOnlyList<IBattleEvent> events) {
        if (events.Count == 0)
            return;
        var data = new SyncBattleEvent[events.Count];
        for (int i = 0; i < events.Count; i++)
            data[i] = BattleEventCoder.Encode(events[i]);
        SendReliableBattleEvents(data);
    }

    /// <summary>
    /// 经传输层可靠通道向全部在线玩家广播整帧战斗事件日志。
    /// ReliableOrdered 保证连接内可靠有序，断线重连期间的事件不补发；
    /// 断线会话 NetPlayer 为空，直接跳过。仅房间线程调用。
    /// </summary>
    private void SendReliableBattleEvents(SyncBattleEvent[] events) {
        var writer = new NetDataWriter();
        ReliableMessageFrame.WriteHeader(writer);
        new ReliableBattleEventLog { Events = events }.Serialize(writer);
        var payload = writer.AsReadOnlySpan();
        foreach (var session in _sessions.Values)
            if (session.NetPlayer is { } netPlayer)
                netPlayer.Peer.SendReliableOrdered(payload);
    }

    /// <summary>
    /// 战斗状态同步器：逐单位经 <c>UnitPawn.SyncFrom</c> 把领域权威状态投影到网络载体，
    /// 房间阶段写回 BattleRoomEntity。字段清单与 tick 换算不在本类出现，收敛于同步通道。
    /// 仅房间线程调用，由 BattleLoop 每帧在 Tick 之后显式驱动。
    /// </summary>
    private sealed class BattleStateSynchronizer(BattleRoomServer room) {
        /// <summary>同步战斗世界：单位投影 → 房间阶段。由 BattleLoop.LateUpdate 驱动。</summary>
        public void Sync(BattleScene battleScene) {
            foreach (var unit in battleScene.BattleUnits)
                if (room._pawnByNetId.TryGetValue(unit.UnitId, out var pawn))
                    pawn.SyncFrom(unit);

            if (room._roomEntity is not { } entity)
                return;
            entity.BattlePhase.Value = (byte)battleScene.CurrentPhase;
            if (battleScene.CurrentPhase == BattlePhase.Running)
                entity.BattleStartUnixTime.Value = room._battleScene.BattleStartUnixTime;
        }
    }
}


