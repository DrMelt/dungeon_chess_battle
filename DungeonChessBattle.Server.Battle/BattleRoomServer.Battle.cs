using System.Numerics;
using DungeonChessBattle.Battle.Domain;
using DungeonChessBattle.Battle.Domain.Buffs;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Combat.Hates;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Domain.Movement;
using DungeonChessBattle.Battle.Logic.Buffs;
using DungeonChessBattle.Battle.Logic.Movement;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.Requests;
using DungeonChessBattle.Protocol.Replay;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Protocol;
using DungeonChessBattle.Server.StateStore.Abstractions;
using LiteEntitySystem;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// BattleRoomServer 的初始化：从 Store 自取数据、单位实体与领域单位对称创建、战斗管理与 RPC 处理。
/// 本 partial 的所有方法仅在房间线程执行。
/// 领域权威在 BattleUnit，UnitPawn 为同步载体；移动经移动桥衔接，状态经投影器写 SyncVar。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>
    /// 房间线程首帧初始化：创建根实体、装配移动桥与投影器、
    /// 从 Store 迁移准备期单位、按副本生成敌人。此后 EntityManager 不再被其他线程触碰。
    /// </summary>
    private void InitializeFromStore() {
        var roomEntity = EntityManager.AddEntity<BattleRoomEntity>(e => {
            e.RoomId.Value = RoomId;
            // 注入服务端权威副本键，客户端据此加载对应的环境场景
            e.DungeonKey.Value = _dungeonKey;
        }) ?? throw new InvalidOperationException($"Failed to create BattleRoomEntity for room '{RoomId}'.");
        _roomEntity = roomEntity;

        // 移动桥与投影器在单位创建后装配，读取已建实体映射
        _battleScene.Configure(new EntityMovementBridge(this),
            new SyncVarProjector(this, EntityManager));

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

        // 战斗循环收编进 LES tick 生命周期：Update=ApplyDecisions 先于位移，
        // LateUpdate=Tick 在实体更新后、状态包发送前。
        EntityManager.AddLocalSingleton(new BattleLoop(_battleScene, HandleBattleFrameEvents));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Initialized from store: {UnitCount} units migrated.",
                RoomId, units.Count);
    }

    /// <summary>
    /// 按副本配置的玩家阵营选项解析选择记录的实际阵营；选项缺失属配置故障，响亮失败。
    /// 仅房间线程调用。
    /// </summary>
    private IReadOnlyList<string> ResolvePlayerCamps(UnitSelection selection) {
        var dungeon = DungeonRegistry.Instance.GetByKey(_dungeonKey);
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
        var dungeon = DungeonRegistry.Instance.GetByKey(_dungeonKey);
        if (dungeon == null)
            return;

        foreach (var spawn in dungeon.Enemies) {
            // 敌人生成以注册表权威配置键为准，杜绝错配
            var config = UnitRegistry.Instance.GetByConfig(spawn.Unit)
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
    /// 战斗系数与技能装配在 BattleUnit，投影器写 SyncVar 供客户端展示。仅房间线程调用。
    /// </summary>
    public UnitPawn CreatePawnEntity(string unitName, IReadOnlyList<string> camps, Vector2 spawnPos) {
        // 兜底防御，上游网络入口已校验，这里仅防未来新增路径绕过校验
        if (unitName.Length > EntityConstants.MaxUnitConfigKeyLength)
            unitName = unitName[..EntityConstants.MaxUnitConfigKeyLength];
        if (!CampConstants.IsValidCamps(camps))
            throw new InvalidOperationException(
                $"Invalid camps '{(camps == null ? string.Empty : string.Join(",", camps))}' for unit '{unitName}' in room '{RoomId}'.");

        var entity = EntityManager.AddEntity<UnitPawn>(e => {
            e.UnitName.Value = unitName;
            var campsData = new SyncCampsData();
            campsData.Set(camps);
            e.CampsData.Value = campsData;
            e.Position.Value = spawnPos;
        }) ?? throw new InvalidOperationException($"Failed to create UnitPawn for unit '{unitName}' in room '{RoomId}'.");

        // 订阅该 Pawn 的玩家输入回调；技能/聚焦请求改经 UnitController 可靠通道进入
        entity.InputHandler = OnPawnInput;
        _roomPawns.Add(entity);
        _pawnByNetId[entity.Id] = entity;

        // 领域单位（权威）：战斗世界结算读写，投影器写 SyncVar
        var config = UnitRegistry.Instance.GetByKey(unitName)
            ?? throw new InvalidOperationException($"Unknown unit config key '{unitName}' in room '{RoomId}'.");
        var unit = new BattleUnit {
            UnitNetId = entity.Id,
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
        _battleUnitByNetId[entity.Id] = unit;
        _battleScene.AddUnit(unit);

        // 技能定义供客户端 UnitGameShow 装配展示资源，本地字段不参与网络同步
        entity.Skills = config.Skills;

        // 注入移动管线，Logic 层 MovementResolver，含场景交互。
        // 场景两端口径一致，从同一副本布局构建 Aether 世界，保证预测与权威确定性一致。
        entity.MoveResolver = (pos, dir, speed, dt) =>
            MovementResolver.Move(pos, dir, speed, dt, entity.BodyRadius.Value, _battleScene.MovementScene, entity.Id);

        return entity;
    }

    /// <summary>按网络 ID 查找本房间的 UnitPawn。</summary>
    public UnitPawn? FindPawnById(ushort netId) {
        return _roomPawns.Find(p => p.Id == netId);
    }

    /// <summary>按网络 ID 查找战斗世界领域单位。</summary>
    private BattleUnit? FindBattleUnit(ushort netId) => _battleUnitByNetId.GetValueOrDefault(netId);

    /// <summary>在本房间启动战斗：委托战斗世界阶段机；阶段投影由投影器写载体，战斗开始帧写入回放记录。</summary>
    public void StartBattle() {
        _battleScene.StartBattle();
        _replayRecorder?.SetStartTick(EntityManager.Tick);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Battle started, phase={Phase}", RoomId, _battleScene.CurrentPhase);
    }

    /// <summary>
    /// 处理通过 UnitPawn 实例事件到达的玩家输入：提交战斗世界并按"移动即打断读条"规则消费。
    /// 移动位移仍由 UnitPawn.Update 确定性结算，客户端预测加服务端权威。
    /// </summary>
    private void OnPawnInput(UnitPawn pawn, UnitInputPacket input, float deltaTime) {
        _battleScene.SubmitMove(pawn.Id, input.MoveDirection);
        TryRecordMoveInput(pawn, input);

        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("[RoomId: {RoomId}] PawnInput: {Unit} dir={Dir}, dt={Dt}",
                RoomId, pawn.UnitName.Value, input.MoveDirection, deltaTime);
    }


    /// <summary>
    /// 处理经 UnitController 可靠请求到达的技能施放请求：面向领域单位发起读条。
    /// 返回值作为请求回执发回客户端。
    /// </summary>
    private bool HandleCastSkillRequest(UnitPawn casterPawn, CastSkillRequest req) {
        if (_battleScene.CurrentPhase != BattlePhase.Running) {
            _logger.LogWarning("[RoomId: {RoomId}] Skill request dropped: battle not running.", RoomId);
            return false;
        }

        if (FindBattleUnit(casterPawn.Id) is not { } caster)
            return false;

        BattleUnit? target = null;
        Vector2? targetPos = null;
        if (req.TargetNetId != 0) {
            target = FindBattleUnit(req.TargetNetId);
            if (target == null) {
                _logger.LogWarning("[RoomId: {RoomId}] Skill request: target unit {TargetId} not found.",
                    RoomId, req.TargetNetId);
                return false;
            }
        }
        else {
            // 位置目标技能，范围伤害，XZ 平面
            targetPos = new Vector2(req.TargetPosX, req.TargetPosZ);
        }

        bool began = _battleScene.BeginCast(caster, new SkillKeyId(req.SkillTypeId), target, targetPos);
        if (!began) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomId: {RoomId}] Skill cast rejected (cooldown): {Caster}, SkillId={SkillId}",
                    RoomId, casterPawn.UnitName.Value, req.SkillTypeId);
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Skill cast began: {Caster} -> {Target}, SkillId={SkillId}",
                RoomId, casterPawn.UnitName.Value, target?.UnitName ?? "(position)", req.SkillTypeId);
        return true;
    }

    /// <summary>
    /// 处理经 UnitController 可靠请求到达的聚焦目标设置：服务端校验目标合法性后写回权威状态。
    /// 0 表示清除聚焦目标；目标必须存在且存活；允许目标为自己。仅影响展示，不经战斗世界。
    /// </summary>
    private bool HandleSetFocusTargetRequest(UnitPawn pawn, ushort targetNetId) {
        if (targetNetId != 0) {
            var targetUnit = FindBattleUnit(targetNetId);
            if (targetUnit == null || targetUnit.Health <= 0f) {
                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("[RoomId: {RoomId}] Focus target rejected: {Unit} -> target {TargetId} not found or dead.",
                        RoomId, pawn.UnitName.Value, targetNetId);
                return false;
            }
        }

        pawn.FocusTargetNetId.Value = targetNetId;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomId: {RoomId}] Focus target set: {Unit} -> {TargetId}",
                RoomId, pawn.UnitName.Value, targetNetId);
        return true;
    }

    /// <summary>清空所有 Pawn 中对指定单位 ID 的聚焦目标，目标死亡时调用。</summary>
    private void ClearFocusTargetsTo(ushort unitNetId) {
        foreach (var pawn in _roomPawns) {
            if (pawn.FocusTargetNetId.Value == unitNetId)
                pawn.FocusTargetNetId.Value = 0;
        }
    }

    /// <summary>
    /// 整帧领域事件处理：死亡单位清空移动输入与清理他人聚焦，再把本帧事件日志整帧编码可靠外送。
    /// 生命、读条、冷却与 Buff 已由投影器在 Tick 内写 SyncVar，房间级阶段已由投影器写载体。
    /// </summary>
    private void HandleBattleFrameEvents(IReadOnlyList<IBattleEvent> events) {
        // 权威状态写回：死亡单位清空移动输入并清理他人聚焦
        foreach (var battleEvent in events) {
            if (battleEvent is UnitDied died) {
                var deadPawn = FindPawnById(died.UnitNetId);
                deadPawn?.SetMovementInput(Vector2.Zero);
                ClearFocusTargetsTo(died.UnitNetId);
            }
        }

        // 整帧事件日志编码经可靠通道外送，空帧不发
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
    /// 战斗世界状态投影器：把领域单位权威状态写回 UnitPawn SyncVar，房间阶段写回 BattleRoomEntity。
    /// 标量字段直接写值（LES 仅在变化时发包）；冷却/Buff/仇恨列表内容比较节流，仅变化时重建 SyncList。
    /// 仅房间线程调用（BattleScene.Tick 末尾）。
    /// </summary>
    private sealed class SyncVarProjector(BattleRoomServer room, ServerEntityManager entityManager) : IBattleProjector {
        public void Project(IReadOnlyList<BattleUnit> units, BattlePhase phase) {
            foreach (var unit in units)
                if (room._pawnByNetId.TryGetValue(unit.UnitNetId, out var pawn))
                    ProjectUnit(unit, pawn);

            if (room._roomEntity is not { } entity)
                return;
            entity.BattlePhase.Value = (byte)phase;
            entity.IsFinished.Value = phase == BattlePhase.Finished;
            if (phase == BattlePhase.Running)
                entity.BattleStartUnixTime.Value = room._battleScene.BattleStartUnixTime;
        }

        private void ProjectUnit(BattleUnit unit, UnitPawn pawn) {
            pawn.Health.Value = unit.Health;
            pawn.MaxHealth.Value = unit.MaxHealth;
            pawn.UnitState.Value = unit.Health <= 0f ? (byte)1 : (byte)0;
            pawn.SkillCasting.Value = unit.SkillCasting.Id;
            pawn.SkillCastRemaining.Value = unit.SkillCastRemaining;
            pawn.GcdEndServerTick.Value = SyncTickHelper.EndTick(entityManager, unit.GcdRemaining);
            pawn.PhysicalAttackBase.Value = unit.PhysicalAttackBase;
            pawn.PhysicalTakePercent.Value = unit.PhysicalTakePercent;
            pawn.MagicAttackBase.Value = unit.MagicAttackBase;
            pawn.MagicTakePercent.Value = unit.MagicTakePercent;
            pawn.CureIntensity.Value = unit.CureIntensity;
            pawn.BaseSpeed.Value = unit.BaseSpeed;
            pawn.BodyRadius.Value = unit.BodyRadius;
            ProjectCooldowns(unit, pawn);
            ProjectBuffs(unit, pawn);
            ProjectHates(unit, pawn);
        }

        /// <summary>个体冷却全量投影，内容一致时跳过，避免每帧重建 SyncList 产生网络流量。</summary>
        private void ProjectCooldowns(BattleUnit unit, UnitPawn pawn) {
            var cds = unit.RuntimeState.Cooldowns;
            bool changed = pawn.SkillCooldowns.Count != cds.Count;
            if (!changed) {
                for (int i = 0; i < cds.Count; i++) {
                    var existing = pawn.SkillCooldowns[i];
                    if (existing.SkillId != cds[i].SkillKey.Id
                        || existing.EndServerTick != SyncTickHelper.EndTick(entityManager, cds[i].Remaining)) {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed)
                return;

            while (pawn.SkillCooldowns.Count > 0)
                pawn.SkillCooldowns.RemoveAt(pawn.SkillCooldowns.Count - 1);
            foreach (var cd in cds)
                pawn.SkillCooldowns.Add(new SyncSkillCooldown {
                    SkillId = cd.SkillKey.Id,
                    EndServerTick = SyncTickHelper.EndTick(entityManager, cd.Remaining),
                });
        }

        /// <summary>Buff 全量投影，内容一致时跳过。</summary>
        private void ProjectBuffs(BattleUnit unit, UnitPawn pawn) {
            var buffs = unit.RuntimeState.Buffs;
            bool changed = pawn.BuffsList.Count != buffs.Count;
            if (!changed) {
                for (int i = 0; i < buffs.Count; i++) {
                    var existing = pawn.BuffsList[i];
                    var b = buffs[i].Instance;
                    if (existing.BuffTypeId != b.BuffTypeId
                        || existing.EndServerTick != SyncTickHelper.EndTick(entityManager, (float)b.Remaining)
                        || existing.StackCount != b.Stacks
                        || existing.MaxStackCount != b.MaxStacks) {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed)
                return;

            while (pawn.BuffsList.Count > 0)
                pawn.BuffsList.RemoveAt(pawn.BuffsList.Count - 1);
            foreach (var buff in buffs)
                pawn.BuffsList.Add(new SyncBuffData {
                    BuffTypeId = buff.Instance.BuffTypeId,
                    EndServerTick = SyncTickHelper.EndTick(entityManager, (float)buff.Instance.Remaining),
                    StackCount = (ushort)buff.Instance.Stacks,
                    MaxStackCount = (ushort)Math.Max(1, buff.Instance.MaxStacks),
                    SourceUnitNetId = buff.Instance.FromNetId,
                    DamageType = EffectDamageType(buff.Effect),
                });
        }


        /// <summary>仇恨全量投影，内容一致时跳过。</summary>
        private void ProjectHates(BattleUnit unit, UnitPawn pawn) {
            var hates = unit.RuntimeState.Hates.Snapshot();
            bool changed = pawn.HatesList.Count != hates.Count;
            if (!changed) {
                for (int i = 0; i < hates.Count; i++) {
                    var existing = pawn.HatesList[i];
                    if (existing.TargetUnitNetId != hates[i].TargetNetId
                        || existing.HateValue != hates[i].Value) {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed)
                return;

            while (pawn.HatesList.Count > 0)
                pawn.HatesList.RemoveAt(pawn.HatesList.Count - 1);
            foreach (var hate in hates)
                pawn.HatesList.Add(new SyncHateData { TargetUnitNetId = hate.TargetNetId, HateValue = hate.Value });
        }

        private static byte EffectDamageType(IBuffEffect effect) => effect switch {
            DotEffect dot => (byte)dot.DamageType,
            _ => 0,
        };
    }

    /// <summary>
    /// 战斗世界移动衔接：位置读实体 SyncVar 回写领域单位，AI 移动输入写实体移动输入。
    /// 仅房间线程调用。
    /// </summary>
    private sealed class EntityMovementBridge(BattleRoomServer room) : IBattleMovementBridge {
        public Vector2 GetPosition(ushort netId) =>
            room._pawnByNetId.TryGetValue(netId, out var pawn) ? pawn.Position.Value : Vector2.Zero;

        public void SetMoveInput(ushort netId, Vector2 moveDirection) {
            if (room._pawnByNetId.TryGetValue(netId, out var pawn))
                pawn.SetMovementInput(moveDirection);
        }
    }
}

