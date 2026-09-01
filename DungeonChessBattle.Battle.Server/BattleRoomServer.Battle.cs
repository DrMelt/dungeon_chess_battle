using System.Numerics;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Entities.Requests;
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
        byte playerIndex = 0;
        foreach (var selection in units) {
            // 玩家阵营由副本配置按选项键权威解析；首个阵营为主阵营，作为出生点分边依据
            var camps = ResolvePlayerCamps(selection);
            var spawnPos = camps[0] == CampConstants.CampA
                ? new Vector2(campAIndex++ * SpawnSpacing, 0)
                : new Vector2(5f + campBIndex++ * SpawnSpacing, 0);
            var pawn = CreatePawnEntity(selection.UnitConfigKey, camps, spawnPos);
            _pawnByPlayerId[selection.PlayerId] = pawn;
            // 回放玩家表与索引：序号即 playerIndex，敌人与非玩家单位不收录
            _playerIndexByNetId[pawn.Id] = playerIndex;
            playerInfos.Add(new ReplayPlayerInfo(selection.PlayerId, selection.PlayerName,
                selection.UnitConfigKey, selection.CampOptionKey, spawnPos.X, spawnPos.Y, pawn.Id));
            playerIndex++;
        }

        // 按房间选中的副本配置生成敌人，阵营由副本配置统一编队，服务端 AI 驱动
        SpawnDungeonEnemies();

        // 战斗输入回放记录：全部单位创建完成后装配，NextNetId 供回放端从玩家之后对齐敌人 ID
        CreateReplayRecorder(playerInfos);
        ushort nextNetId = playerInfos.Count > 0
            ? (ushort)(playerInfos[^1].NetId + 1)
            : (ushort)(1 + 1);
        _replayRecorder?.SetNextNetId(nextNetId);

        // 战斗循环收编进 LES tick 生命周期：Update=输入预备（AI 决策 → 在架施法重试）先于位移，
        // LateUpdate=Tick → 状态同步 → 整帧事件外送。
        EntityManager.AddLocalSingleton(new BattleLoop(_battleScene, _intentHub,
            scene => _stateSynchronizer.Sync(scene), HandleBattleFrameEvents));

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
        var unit = new BattleUnit {
            UnitId = entity.Id,
            UnitName = unitName,
            Camps = camps,
            Skills = config.Skills,
            Intelligence = config.Intelligence,
            HateRule = config.HateRule,
            HateFactor = config.HateFactor,
            MaxHealth = config.MaxHealth,
            Health = config.MaxHealth,
            PhysicalAttackBase = config.PhysicalAttackBase,
            PhysicalTakePercent = config.PhysicalTakePercent,
            MagicAttackBase = config.MagicAttackBase,
            MagicTakePercent = config.MagicTakePercent,
            CureIntensity = config.CureIntensity,
            BaseSpeed = config.BaseSpeed,
            BodyRadius = config.BodyRadius,
            Position = spawnPos,
        };
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

    /// <summary>
    /// 处理通过 UnitPawn 实例事件到达的玩家输入：经输入门面提交移动意图并旁路记录到回放，
    /// 位移由领域 BattleScene.Tick 统一结算。
    /// </summary>
    private void OnPawnInput(UnitPawn pawn, UnitInputPacket input, float deltaTime) {
        _intentHub.SubmitMove(pawn.Id, input.MoveDirection);
        TryRecordMoveInput(pawn, input);

        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("[RoomId: {RoomId}] PawnInput: {Unit} dir={Dir}, dt={Dt}",
                RoomId, pawn.UnitKeyName.Value, input.MoveDirection, deltaTime);
    }

    /// <summary>
    /// 处理经 UnitController 可靠请求到达的施法请求：交输入门面投递意图，
    /// 施法者与目标的 ID 解析和排队都在门面内完成，房间不再另做一遍。返回值作为回执发回客户端。
    /// </summary>
    private bool HandleCastSkillRequest(UnitPawn casterPawn, CastSkillRequest req) {
        if (_battleScene.CurrentPhase != BattlePhase.Running) {
            _logger.LogWarning("[RoomId: {RoomId}] Skill request dropped: battle not running.", RoomId);
            return false;
        }

        if (string.IsNullOrEmpty(req.SkillTypeId) || req.SkillTypeId.Length > SkillKeyId.MaxKeyLength) {
            _logger.LogWarning("[RoomId: {RoomId}] Skill request dropped: skill key invalid or too long.", RoomId);
            return false;
        }

        // TargetNetId 为 0 走位置目标（范围技能，XZ 平面），非 0 走单位目标
        Vector2? targetPos = req.TargetNetId != 0 ? null : new Vector2(req.TargetPosX, req.TargetPosZ);

        // 投递失败只剩一种成因：施法者或目标解析不到
        if (!_intentHub.SubmitCast(casterPawn.Id, new SkillKeyId(req.SkillTypeId), req.TargetNetId, targetPos)) {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RoomId: {RoomId}] Skill request not delivered: {Caster} or target {Target} missing.",
                    RoomId, casterPawn.UnitKeyName.Value, req.TargetNetId);
            return false;
        }

        // 入排队槽的意图由排队器另记 LogCastQueued
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Skill request taken: {Caster} -> {Target}, SkillId={SkillId}",
                RoomId, casterPawn.UnitKeyName.Value,
                req.TargetNetId == 0 ? "(position)" : req.TargetNetId.ToString(), req.SkillTypeId);
        return true;
    }

    /// <summary>
    /// 处理经 UnitController 可靠请求到达的聚焦目标设置：服务端校验目标合法性后写回权威状态。
    /// 0 表示清除聚焦目标；目标必须存在且存活；允许目标为自己。仅影响展示，不经战斗世界。
    /// 设置后目标死亡由投影期的 <see cref="ClearDeadFocusTargets"/> 清 0，不依赖死亡事件。
    /// </summary>
    private bool HandleSetFocusTargetRequest(UnitPawn pawn, ushort targetNetId) {
        if (targetNetId != 0 && _battleScene.FindUnit(targetNetId) is not { IsDead: false }) {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RoomId: {RoomId}] Focus target rejected: {Unit} -> target {TargetId} not found or dead.",
                    RoomId, pawn.UnitKeyName.Value, targetNetId);
            return false;
        }

        pawn.FocusTargetNetId.Value = targetNetId;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomId: {RoomId}] Focus target set: {Unit} -> {TargetId}",
                RoomId, pawn.UnitKeyName.Value, targetNetId);
        return true;
    }

    /// <summary>
    /// 维持"聚焦目标必存活"不变式：聚焦指向不存在或已死亡单位时清 0。
    /// 与 HandleSetFocusTargetRequest 的设置期校验同源，随状态投影每帧收敛，不依赖死亡事件。
    /// </summary>
    private void ClearDeadFocusTargets() {
        foreach (var pawn in _roomPawns) {
            var targetNetId = pawn.FocusTargetNetId.Value;
            if (targetNetId == 0)
                continue;
            if (_battleScene.FindUnit(targetNetId) is { IsDead: false })
                continue;
            pawn.FocusTargetNetId.Value = 0;
        }
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
        /// <summary>同步战斗世界：单位投影 → 聚焦清活 → 房间阶段。由 BattleLoop.LateUpdate 驱动。</summary>
        public void Sync(BattleScene battleScene) {
            foreach (var unit in battleScene.BattleUnits)
                if (room._pawnByNetId.TryGetValue(unit.UnitId, out var pawn))
                    pawn.SyncFrom(unit);

            // 死亡无事件通道，聚焦清活随投影按生命值收敛
            room.ClearDeadFocusTargets();

            if (room._roomEntity is not { } entity)
                return;
            entity.BattlePhase.Value = (byte)battleScene.CurrentPhase;
            if (battleScene.CurrentPhase == BattlePhase.Running)
                entity.BattleStartUnixTime.Value = room._battleScene.BattleStartUnixTime;
        }
    }
}


