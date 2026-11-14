using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.Runtime.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Inputs;
using DungeonChessBattle.Battle.Config.Shared.Content;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Entities.SyncData;
using DungeonChessBattle.Replay.Shared;
using DungeonChessBattle.Server.DataStore.Shared;
using ErrorOr;
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
    /// 从 Store 迁移准备期单位、按副本生成敌人。此后 EntityManager 不被其他线程触碰。
    /// 内容与配置裁决、实体数量上限与回放轨道容量不通过都以错误返回，调用方据此清理房间；
    /// LES 与 CLR 交界的异常不在此收口。
    /// </summary>
    private ErrorOr<Success> InitializeFromStore() {
        if (EntityManager.AddEntity<BattleRoomEntity>(e => {
            e.RoomId.Value = RoomId;
            // 注入服务端权威副本键，客户端据此加载对应的环境场景
            e.DungeonKey.Value = _dungeonKey;
        }) is not { } roomEntity)
            return BattleRoomErrors.RoomEntityLimitReached(RoomId);
        _roomEntity = roomEntity;

        // 状态同步器在单位创建后装配，读取已建实体映射；由 BattleLoop 每帧在 Tick 之后显式驱动
        _stateSynchronizer = new BattleStateSynchronizer(this);

        // 从 Store 迁移准备期单位；同选项按序错开出生点，避免同阵营单位重叠
        var units = _stateStore.GetPrepareUnits(RoomId);
        var playerInfos = new List<ReplayPlayerInfo>(units.Count);
        var spawnIndexByOption = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var selection in units) {
            // 玩家阵营与出生列由副本配置按选项键权威解析，本层不做阵营名判定
            var resolved = ResolvePlayerCampOption(selection);
            if (resolved.IsError)
                return resolved.FirstError;
            var option = resolved.Value;

            int spawnIndex = spawnIndexByOption.GetValueOrDefault(option.Key);
            spawnIndexByOption[option.Key] = spawnIndex + 1;
            var spawnPos = new Vector2(option.SpawnBaseX + spawnIndex * option.SpawnXSpacing, 0);
            var pawn = CreatePawnEntity(selection.UnitConfigKey, option.Camps, spawnPos);
            if (pawn.IsError)
                return pawn.FirstError;

            // 玩家输入轨道由生成路径登记，与敌人同源分流
            _intentDriver.RegisterPlayer(pawn.Value.Id);
            _pawnByPlayerId[selection.PlayerId] = pawn.Value;
            // 回放玩家表：下标即记录条目里的玩家序号，敌人与非玩家单位不收录
            playerInfos.Add(new ReplayPlayerInfo(selection.PlayerName, selection.UnitConfigKey, pawn.Value.Id));
        }

        // 按房间选中的副本配置生成敌人，阵营由副本配置统一编队，意图源按各单位配置声明的控制者登记
        var enemies = SpawnDungeonEnemies();
        if (enemies.IsError)
            return enemies.FirstError;

        // 战斗输入回放记录：全部单位创建完成后装配，单位初始态整表落盘，敌人 ID 取记录值；
        // 条目引用了表外单位时门内解析落空，不报错
        var recorder = CreateReplayRecorder(playerInfos);
        if (recorder.IsError)
            return recorder.FirstError;
        RecordUnitInits();

        // 战斗循环收编进 LES tick 生命周期：LateUpdate=输入预备（刷新意图源 → 投递当帧意图）→ Tick →
        // 帧末收口（权威状态同步 + 把结束帧写进回放时间轴）→ 整帧事件外送。
        // 输入预备排在实体更新之后：本 tick 到达的玩家输入当帧即生效，与回放端「注入 → 预备 → Tick」同序。
        // 帧末收口留在闭包里：_stateSynchronizer 在上方刚赋值，闭包带得走这份可空状态。
        EntityManager.AddLocalSingleton(new BattleLoop(_battleScene, _intentHub,
            scene => {
                _stateSynchronizer.Sync(scene);
                RecordBattleEnd(scene);
            }, HandleBattleFrameEvents));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Initialized from store: {UnitCount} units migrated.",
                RoomId, units.Count);

        return Result.Success;
    }

    /// <summary>
    /// 按副本配置的玩家阵营选项解析选择记录对应的选项，阵营与出生列一并取自该选项；
    /// 选项缺失属准备记录与副本配置不一致，以错误交回房间初始化。仅房间线程调用。
    /// </summary>
    private ErrorOr<PlayerCampOption> ResolvePlayerCampOption(UnitSelection selection) {
        // 副本配置由启动方解析并随房间传入，选项缺失即准备记录与副本配置不一致
        var option = _dungeon.PlayerCampOptions.FirstOrDefault(o => o.Key == selection.CampOptionKey);
        if (option is null)
            return BattleRoomErrors.UnknownCampOption(_dungeonKey.Value, selection.CampOptionKey);
        return option;
    }

    /// <summary>
    /// 按房间副本配置生成敌人：UnitPawn 与 BattleUnit 对称创建，敌方在场地对侧按纵队排布。
    /// 副本引用的单位配置未注册以错误交回房间初始化。仅房间线程调用。
    /// </summary>
    private ErrorOr<Success> SpawnDungeonEnemies() {
        foreach (var spawn in _dungeon.Enemies) {
            // 敌人生成以注册表权威配置键为准，杜绝错配
            var config = _content.GetUnit(spawn.Unit.ConfigKey);
            if (config is null)
                return BattleRoomErrors.UnknownUnitConfig(_dungeonKey.Value, spawn.Unit.ConfigKey.Value);

            for (int i = 0; i < spawn.Count; i++) {
                var spawnPos = new Vector2(spawn.SpawnBaseX + i * spawn.SpawnXSpacing, 0);
                var pawn = CreatePawnEntity(config.ConfigKey, _dungeon.EnemyCamps, spawnPos);
                if (pawn.IsError)
                    return pawn.FirstError;

                // 未声明控制者即不登记意图源，该单位不产出意图
                if (config.Controller is { } controller)
                    _intentDriver.RegisterAutonomous(pawn.Value.Id, controller);
            }
        }

        return Result.Success;
    }

    /// <summary>
    /// 在本房间的 SEM 中创建 UnitPawn 实体，按同一 NetId 创建领域单位 BattleUnit 注册进战斗世界。
    /// 战斗系数与技能装配在 BattleUnit，状态同步器写 SyncVar 供客户端展示；意图源不在本方法登记，
    /// 由调用方按单位进入战场的方式选择驱动轨道。
    /// 阵营列表与单位配置都取自内容，在进实体初始化委托之前裁决：该委托签名无返回值，进去只能抛。
    /// 仅房间线程调用。
    /// </summary>
    public ErrorOr<UnitPawn> CreatePawnEntity(UnitConfigKey unitName, IReadOnlyList<CampId> camps, Vector2 spawnPos) {
        if (camps.Count is 0 or > SyncCampsData.MaxCamps || camps.Any(camp => camp.IsDefault))
            return BattleRoomErrors.InvalidCamps(unitName.Value, camps.Count);

        var config = _content.GetUnit(unitName);
        if (config is null)
            return BattleRoomErrors.UnknownUnitConfig(_dungeonKey.Value, unitName.Value);

        if (EntityManager.AddEntity<UnitPawn>(e => {
            e.UnitKeyName.Value = unitName;
            var campsData = new SyncCampsData();
            campsData.Set(camps);
            e.CampsData.Value = campsData;
            e.Position.Value = spawnPos;
        }) is not { } entity)
            return BattleRoomErrors.UnitEntityLimitReached(RoomId, unitName.Value);

        // 订阅该 Pawn 的玩家输入回调；技能/聚焦请求改经 UnitController 可靠通道进入
        entity.InputHandler = OnPawnInput;
        _roomPawns.Add(entity);
        _pawnByNetId[entity.Id] = entity;

        // 领域单位（权威）：战斗世界结算读写，状态同步器写 SyncVar
        var unit = BattleUnitFactory.Create(config, entity.Id, camps, spawnPos);
        _battleScene.AddUnit(unit);

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
    /// 整帧领域事件日志整帧编码经可靠通道外送，空帧不发；未登记映射的事件类型记一条错误并跳过该条。
    /// 单位与房间级状态已由 BattleStateSynchronizer 在 Tick 后写 SyncVar；死亡不走事件，
    /// 由生命值下行派生，断线重连后随状态自愈。仅房间线程调用。
    /// </summary>
    private void HandleBattleFrameEvents(IReadOnlyList<IBattleEvent> events) {
        if (events.Count == 0)
            return;
        var data = new SyncBattleEvent[events.Count];
        int count = 0;
        foreach (var e in events) {
            var encoded = BattleEventCoder.Encode(e);
            if (encoded.IsError) {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.LogError("[RoomId: {RoomId}] 事件编码失败：{Reason}", RoomId, encoded.FirstError.Description);
                continue;
            }
            data[count++] = encoded.Value;
        }
        if (count == 0)
            return;
        if (count < data.Length)
            data = data[..count];
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
            foreach (var unit in battleScene.AuthorityUnits)
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


