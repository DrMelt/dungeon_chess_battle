using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Domain.Movement;
using DungeonChessBattle.Battle.Logic.Movement;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.Requests;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Protocol;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// BattleRoomServer 的初始化，从 Store 自取数据、单位实体创建、战斗管理与 RPC 处理。
/// 本 partial 的所有方法仅在房间线程执行。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>
    /// 房间线程首帧初始化：创建根实体、订阅实体事件、创建 Logic 房间、
    /// 从 Store 迁移准备期单位。此后 EntityManager 不再被其他线程触碰。
    /// </summary>
    private void InitializeFromStore() {
        // 在房间 SEM 中创建 BattleRoomEntity，并订阅其实例事件
        _roomEntity = EntityManager.AddEntity<BattleRoomEntity>(e => {
            e.RoomId.Value = RoomId;
        }) ?? throw new InvalidOperationException($"Failed to create BattleRoomEntity for room '{RoomId}'.");

        // 注入服务端权威创建时间，房间在构造时创建，此处直接取用；
        // 且不能在 AddEntity initAction 中注入——OnConstructed 会以默认值覆盖运行时值）
        _roomEntity?.CreatedUnixTime.Value =
                new DateTimeOffset(_roomCreatedAt).ToUnixTimeSeconds();

        // 注入服务端权威副本键，客户端据此加载对应的环境场景
        _roomEntity?.DungeonKey.Value = _dungeonKey;
        // 构建移动物理场景：按副本键取战场布局，静态障碍写入 Aether World；单位后续注册进场景做互斥
        _movementScene = new PhysicsMovementScene(DungeonRegistry.Instance.GetMovementLayout(_dungeonKey));

        // 从 Store 迁移准备期单位；同阵营按序错开出生点，避免重名/同阵营单位重叠
        var units = _stateStore.GetPrepareUnits(RoomId);
        int campAIndex = 0, campBIndex = 0;
        foreach (var selection in units) {
            var spawnPos = selection.Camp == CampConstants.CampA
                ? new Vector2(campAIndex++ * SpawnSpacing, 0)
                : new Vector2(5f + campBIndex++ * SpawnSpacing, 0);
            var pawn = CreatePawnEntity(selection.UnitName, selection.Camp, spawnPos);
            _pawnByPlayerId[selection.PlayerId] = pawn;
        }

        // 按房间选中的副本配置生成敌人（Camp_BOSS 阵营，服务端 AI 驱动）
        SpawnDungeonEnemies();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Initialized from store: {UnitCount} units migrated, {EnemyCount} enemies spawned.",
                RoomId, units.Count, _enemyPawns.Count);
    }

    /// <summary>
    /// 按房间副本配置生成敌人 Pawn。敌方在场地对侧按纵队排布，阵营固定 CampBoss。
    /// 仅房间线程调用。
    /// </summary>
    private void SpawnDungeonEnemies() {
        var dungeon = DungeonRegistry.Instance.GetByKey(_dungeonKey);
        if (dungeon == null)
            return;

        foreach (var spawn in dungeon.Enemies) {
            // 敌人生成以注册表权威配置键为准，玩家/敌人沿用同一身份映射，杜绝错配
            var config = UnitRegistry.Instance.GetByConfig(spawn.Unit)
                ?? throw new InvalidOperationException(
                    $"Dungeon '{_dungeonKey}' references unregistered unit config for enemy spawn.");
            for (int i = 0; i < spawn.Count; i++) {
                var spawnPos = new Vector2(spawn.SpawnBaseX + i * spawn.SpawnXSpacing, 0);
                var pawn = CreatePawnEntity(config.ConfigKey, config.Camp!, spawnPos);
                _enemyPawns.Add(pawn);
            }
        }
    }

    /// <summary>
    /// 在本房间的 SEM 中创建 UnitPawn 实体。仅房间线程调用。
    /// </summary>
    public UnitPawn CreatePawnEntity(string unitName, string camp, Vector2 spawnPos) {
        // 兜底防御，上游网络入口已校验，这里仅防未来新增路径绕过校验
        if (unitName.Length > EntityConstants.MaxUnitNameLength)
            unitName = unitName[..EntityConstants.MaxUnitNameLength];
        if (!CampConstants.IsValidCamp(camp))
            throw new InvalidOperationException($"Invalid camp '{camp}' for unit '{unitName}' in room '{RoomId}'.");

        var entity = EntityManager.AddEntity<UnitPawn>(e => {
            e.UnitName.Value = unitName;
            e.Camp.Value = camp;
            e.Position.Value = spawnPos;
        }) ?? throw new InvalidOperationException($"Failed to create UnitPawn for unit '{unitName}' in room '{RoomId}'.");

        // 订阅该 Pawn 的玩家输入回调；技能/聚焦请求改经 UnitController 可靠通道进入
        entity.InputHandler = OnPawnInput;

        _roomPawns.Add(entity);

        // 从单位配置注入 Pawn 战斗系数，权威由 BattleRoom 直接读写 IBattleUnit 载体
        var config = UnitRegistry.Instance.GetByKey(unitName);
        if (config != null) {
            entity.MaxHealth.Value = config.MaxHealth;
            entity.Health.Value = config.MaxHealth;
            entity.PhysicalAttackBase.Value = config.PhysicalAttackBase;
            entity.PhysicalTakePercent.Value = config.PhysicalTakePercent;
            entity.MagicAttackBase.Value = config.MagicAttackBase;
            entity.MagicTakePercent.Value = config.MagicTakePercent;
            entity.CureIntensity.Value = config.CureIntensity;
            entity.BaseSpeed.Value = config.BaseSpeed;
            entity.BodyRadius.Value = config.BodyRadius;
            entity.Skills = config.Skills;
        }

        // 注册到战斗编排门面，BattleRoom 面向 IBattleUnit 结算，读条、冷却与 Buff 由 Tick 写回 Pawn
        _battleRoom.AddUnit(entity);

        // 注入碰撞半径与移动管线，Logic 层 MovementResolver，含场景交互。
        // 场景两端口径一致，从同一副本布局构建 Aether 世界，保证预测与权威确定性一致；
        // 半径与位置延迟读取，规避实体构造时同步未完成的时序。
        var scene = _movementScene ?? throw new InvalidOperationException($"Room '{RoomId}' movement scene not initialized.");
        entity.MoveResolver = (pos, dir, speed, dt) =>
            MovementResolver.Move(pos, dir, speed, dt, entity.BodyRadius.Value, scene, entity.Id);
        scene.AddActor(entity.Id, () => entity.BodyRadius.Value, () => entity.Position.Value);

        return entity;
    }

    /// <summary>
    /// 在本房间范围内按 NetId 查找 UnitPawn，不再跨房间查找。
    /// </summary>
    public UnitPawn? FindPawnById(ushort netId) {
        return _roomPawns.Find(p => p.Id == netId);
    }

    /// <summary>
    /// 在本房间启动战斗：委托 BattleRoom 阶段机，并将阶段事件翻译为网络同步。
    /// </summary>
    public void StartBattle() {
        foreach (var e in _battleRoom.StartBattle())
            HandleDomainEvent(e);
    }

    /// <summary>
    /// 处理通过 UnitPawn 实例事件到达的玩家输入。
    /// 移动已由 UnitPawn.Update() 确定性结算，客户端预测加服务端权威，
    /// 本方法仅作服务端输入钩子，保留日志与未来扩展。
    /// </summary>
    private void OnPawnInput(UnitPawn pawn, UnitInputPacket input, float deltaTime) {
        // 移动已由 UnitPawn.Update() 确定性结算，客户端预测加服务端权威。
        // 此处驱动移动即打断读条，BattleRoom 保留既有行为。
        _battleRoom.OnUnitMoved(pawn, input.MoveDirection);

        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("[RoomId: {RoomId}] PawnInput: {Unit} dir={Dir}, dt={Dt}",
                RoomId, pawn.UnitName.Value, input.MoveDirection, deltaTime);
    }

    /// <summary>
    /// 处理经 UnitController 可靠请求到达的技能施放请求：发起读条。
    /// 服务端设置施法状态，SkillCasting 与 SkillCastRemaining，读条由房间 tick 推进，
    /// 完成时 Logic 结算并回写。返回值作为请求回执发回客户端。
    /// </summary>
    private bool HandleCastSkillRequest(UnitPawn casterPawn, CastSkillRequest req) {
        if (_battleRoom.CurrentPhase != BattlePhase.Running) {
            _logger.LogWarning("[RoomId: {RoomId}] Skill request dropped: battle not running.", RoomId);
            return false;
        }

        // 发起读条：BattleRoom 面向 IBattleUnit 校验冷却并写入读条状态
        IBattleUnit? target = null;
        Vector2? targetPos = null;
        if (req.TargetNetId != 0) {
            var targetPawn = FindPawnById(req.TargetNetId);
            if (targetPawn == null) {
                _logger.LogWarning("[RoomId: {RoomId}] Skill request: target pawn {TargetId} not found.",
                    RoomId, req.TargetNetId);
                return false;
            }
            target = targetPawn;
        }
        else {
            // 位置目标技能，范围伤害，XZ 平面
            targetPos = new Vector2(req.TargetPosX, req.TargetPosZ);
        }

        bool began = _battleRoom.BeginCast(casterPawn, new SkillKeyId(req.SkillTypeId), target, targetPos);
        if (!began) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomId: {RoomId}] Skill cast rejected (cooldown): {Caster}, SkillId={SkillId}",
                    RoomId, casterPawn.UnitName.Value, req.SkillTypeId);
            return false;
        }
        // 读条状态已由 BeginCast 直接写回 Pawn，无需额外回写

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Skill cast began: {Caster} -> {Target}, SkillId={SkillId}",
                RoomId, casterPawn.UnitName.Value, target?.UnitName ?? "(position)", req.SkillTypeId);
        return true;
    }

    /// <summary>
    /// 处理经 UnitController 可靠请求到达的聚焦目标设置：服务端校验目标合法性后写回权威状态。
    /// 0 表示清除聚焦目标；目标必须存在且存活；允许目标为自己。
    /// </summary>
    private bool HandleSetFocusTargetRequest(UnitPawn pawn, ushort targetNetId) {
        if (targetNetId != 0) {
            var targetPawn = FindPawnById(targetNetId);
            if (targetPawn == null || targetPawn.UnitState.Value == 1) {
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
    /// 领域事件 → 网络翻译：把 BattleRoom 产出的 IDomainEvent 转换为 RPC / SyncVar 写回。
    /// Health、读条、冷却与 Buff 全量已由 BattleRoom 直接写 IBattleUnit 的 Pawn SyncVar，
    /// 此处仅处理瞬时事件，阶段、受击、死亡、Buff 增减。
    /// </summary>
    private void HandleDomainEvent(IDomainEvent domainEvent) {
        switch (domainEvent) {
            case BattleStarted:
                if (_roomEntity != null) {
                    _roomEntity.BattlePhase.Value = (byte)BattlePhase.Running;
                    _roomEntity.IsFinished.Value = false;
                }
                break;

            case BattleEnded ended:
                if (_roomEntity != null) {
                    _roomEntity.BattlePhase.Value = (byte)BattlePhase.Finished;
                    _roomEntity.IsFinished.Value = true;
                    _roomEntity.WinnerCamp.Value = ended.WinnerCamp ?? string.Empty;
                }
                break;

            case DamageOccurred dmg:
                FindPawnById(dmg.TargetNetId)?
                    .BroadcastDamageTaken(dmg.AppliedDamage, dmg.DamageType);
                break;

            case HealOccurred:
                // 治疗量经 Health SyncVar 自动同步，无需额外 RPC
                break;

            case UnitDied died:
                var deadPawn = FindPawnById(died.UnitNetId);
                deadPawn?.UnitState.Value = 1;
                ClearFocusTargetsTo(died.UnitNetId);
                break;

            case BuffApplied buff:
                BroadcastBuffChanged(buff.TargetNetId, buff.BuffTypeId, (ushort)buff.StackCount, added: true);
                break;

            case BuffExpired buffExp:
                BroadcastBuffChanged(buffExp.TargetNetId, buffExp.BuffTypeId, 0, added: false);
                break;

            case CastCompleted:
                // 日志级事件，无网络投影
                break;
        }
    }

    /// <summary>Buff 增减事件：从 Pawn 的同步列表取快照构造 SyncBuffData 并广播 RPC。</summary>
    private void BroadcastBuffChanged(ushort targetNetId, ushort buffTypeId, ushort stackCount, bool added) {
        var pawn = FindPawnById(targetNetId);
        if (pawn == null)
            return;

        var data = pawn.BuffsList.FirstOrDefault(b => b.BuffTypeId == buffTypeId);
        if (data.BuffTypeId == 0)
            data = new SyncBuffData { BuffTypeId = buffTypeId, StackCount = stackCount };

        if (added)
            pawn.BroadcastBuffAdded(data);
        else
            pawn.BroadcastBuffRemoved(data);
    }
}
