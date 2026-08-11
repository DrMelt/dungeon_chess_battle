using System.Numerics;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Protocol;
using DungeonChessBattle.Protocol.Enums;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Battle.Logic.Movement;
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

        _roomEntity.CreateUnitRequested += OnCreateUnitRequested;
        _roomEntity.StartBattleRequested += OnStartBattleRequested;

        // 注入服务端权威创建时间，房间在构造时创建，此处直接取用；
        // 且不能在 AddEntity initAction 中注入——OnConstructed 会以默认值覆盖运行时值）
        _roomEntity?.CreatedUnixTime.Value =
                new DateTimeOffset(_roomCreatedAt).ToUnixTimeSeconds();

        // 从 Store 迁移准备期单位
        var units = _stateStore.GetPrepareUnits(RoomId);
        foreach (var selection in units) {
            var spawnPos = selection.Camp == CampConstants.CampA
                ? new Vector2(0, 0)
                : new Vector2(5, 0);
            CreatePawnEntity(selection.UnitName, selection.Camp, spawnPos);
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Initialized from store: {UnitCount} units migrated.", RoomId, units.Count);
    }

    /// <summary>
    /// 在本房间的 SEM 中创建 UnitPawn 实体。仅房间线程调用。
    /// </summary>
    public UnitPawn CreatePawnEntity(string unitName, string camp, Vector2 spawnPos) {
        // 兜底防御，上游网络入口已校验，这里仅防未来新增路径绕过校验
        if (unitName.Length > EntityConstants.MaxUnitNameLength)
            unitName = unitName[..EntityConstants.MaxUnitNameLength];
        if (!CampConstants.IsValidCamp(camp))
            camp = CampConstants.CampA;

        var entity = EntityManager.AddEntity<UnitPawn>(e => {
            e.UnitName.Value = unitName;
            e.Camp.Value = camp;
            e.Position.Value = spawnPos;
        }) ?? throw new InvalidOperationException($"Failed to create UnitPawn for unit '{unitName}' in room '{RoomId}'.");

        // 订阅该 Pawn 的技能 RPC 与玩家输入回调
        entity.SkillCastRequested += OnPawnSkillCast;
        entity.InputHandler = OnPawnInput;

        _roomPawns.Add(entity);

        // 从单位配置注入 Pawn 战斗系数，权威由 BattleRoom 直接读写 IBattleUnit 载体
        var configEntry = UnitRegistry.Instance.GetByDisplayName(unitName);
        if (configEntry != null) {
            var cfg = configEntry.Config;
            entity.MaxHealth.Value = cfg.MaxHealth;
            entity.Health.Value = cfg.MaxHealth;
            entity.PhysicalAttackBase.Value = cfg.PhysicalAttackBase;
            entity.PhysicalTakePercent.Value = cfg.PhysicalTakePercent;
            entity.MagicAttackBase.Value = cfg.MagicAttackBase;
            entity.MagicTakePercent.Value = cfg.MagicTakePercent;
            entity.CureIntensity.Value = cfg.CureIntensity;
            entity.BaseSpeed.Value = cfg.BaseSpeed;
            entity.BodyRadius.Value = cfg.BodyRadius;
            foreach (var skill in cfg.Skills)
                entity.SkillIds.Add(skill.Id);
        }

        // 注册到战斗编排门面，BattleRoom 面向 IBattleUnit 结算，读条、冷却与 Buff 由 Tick 写回 Pawn
        _battleRoom.AddUnit(entity);

        // 注入碰撞半径与移动管线，Logic 层 MovementResolver，含场景交互。
        // 场景两端口径一致，OpenMovementScene 无碰撞，保证预测与权威确定性一致。
        entity.MoveResolver = (pos, dir, speed, dt) =>
            MovementResolver.Move(pos, dir, speed, dt, entity.BodyRadius.Value, OpenMovementScene.Instance);

        return entity;
    }

    /// <summary>
    /// 在本房间范围内按 NetId 查找 UnitPawn，不再跨房间查找。
    /// </summary>
    public UnitPawn? FindPawnById(ushort netId) {
        return _roomPawns.Find(p => p.Id == netId);
    }

    /// <summary>
    /// 在本房间范围内按单位名称查找 UnitPawn。
    /// </summary>
    public UnitPawn? FindPawnByName(string unitName) {
        return _roomPawns.Find(p => p.UnitName.Value == unitName);
    }

    /// <summary>
    /// 在本房间启动战斗：委托 BattleRoom 阶段机，并将阶段事件翻译为网络同步。
    /// </summary>
    public void StartBattle() {
        foreach (var e in _battleRoom.StartBattle())
            HandleDomainEvent(e);
    }

    /// <summary>
    /// RPC：客户端请求创建单位。
    /// </summary>
    private void OnCreateUnitRequested(BattleRoomEntity roomEntity, SyncCreateUnitRequest req) {
        // 防御：RPC 数据来自网络，必须校验，Put 超限会静默变空串，必须显式拦截
        if (string.IsNullOrWhiteSpace(req.UnitName)
            || req.UnitName.Length > EntityConstants.MaxUnitNameLength
            || !CampConstants.IsValidCamp(req.Camp)) {
            _logger.LogWarning("[RoomServer:{RoomId}] Rejected create unit RPC: name='{Name}', camp='{Camp}'",
                RoomId, req.UnitName, req.Camp);
            return;
        }

        var spawnPos = req.Camp == CampConstants.CampA
            ? new Vector2(0, 0)
            : new Vector2(5, 0);

        CreatePawnEntity(req.UnitName, req.Camp, spawnPos);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Unit created via RPC: {UnitName}, camp={Camp}",
                RoomId, req.UnitName, req.Camp);
    }

    /// <summary>
    /// RPC：客户端请求开始战斗。
    /// </summary>
    private void OnStartBattleRequested(BattleRoomEntity roomEntity) {
        StartBattle();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Battle started via RPC", RoomId);
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

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomServer:{RoomId}] PawnInput: {Unit} dir={Dir}, dt={Dt}",
                RoomId, pawn.UnitName.Value, input.MoveDirection, deltaTime);
    }

    /// <summary>
    /// 处理通过 UnitPawn 实例事件到达的技能施放请求：由瞬发结算改为发起读条。
    /// 服务端设置施法状态，SkillCasting 与 SkillCastRemaining，读条由房间 tick 推进，
    /// 完成时 Logic 结算并回写。
    /// </summary>
    private void OnPawnSkillCast(UnitPawn casterPawn, SyncSkillRequest req) {
        if (_battleRoom.CurrentPhase != BattlePhase.Running) {
            _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC dropped: battle not running.", RoomId);
            return;
        }

        // 发起读条：BattleRoom 面向 IBattleUnit 校验冷却并写入读条状态
        IBattleUnit? target = null;
        Vector2? targetPos = null;
        if (req.TargetUnitNetId != 0) {
            var targetPawn = FindPawnById(req.TargetUnitNetId);
            if (targetPawn == null) {
                _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC: target pawn {TargetId} not found.",
                    RoomId, req.TargetUnitNetId);
                return;
            }
            target = targetPawn;
        }
        else {
            // 位置目标技能，范围伤害，XZ 平面
            targetPos = new Vector2(req.TargetPosX, req.TargetPosZ);
        }

        bool began = _battleRoom.BeginCast(casterPawn, req.SkillTypeId, target, targetPos);
        if (!began) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] Skill cast rejected (cooldown): {Caster}, SkillId={SkillId}",
                    RoomId, casterPawn.UnitName.Value, req.SkillTypeId);
            return;
        }
        // 读条状态已由 BeginCast 直接写回 Pawn，无需额外回写

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Skill cast began: {Caster} -> {Target}, SkillId={SkillId}",
                RoomId, casterPawn.UnitName.Value, target?.UnitName ?? "(position)", req.SkillTypeId);
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
                FindPawnByName(dmg.TargetName)?
                    .BroadcastDamageTaken(dmg.AppliedDamage, dmg.DamageType);
                break;

            case HealOccurred:
                // 治疗量经 Health SyncVar 自动同步，无需额外 RPC
                break;

            case UnitDied died:
                var deadPawn = FindPawnByName(died.UnitName);
                deadPawn?.UnitState.Value = 1;
                break;

            case BuffApplied buff:
                BroadcastBuffChanged(buff.TargetName, buff.BuffTypeId, (ushort)buff.StackCount, added: true);
                break;

            case BuffExpired buffExp:
                BroadcastBuffChanged(buffExp.TargetName, buffExp.BuffTypeId, (ushort)0, added: false);
                break;

            case CastCompleted:
                // 日志级事件，无网络投影
                break;
        }
    }

    /// <summary>Buff 增减事件：从 Pawn 的同步列表取快照构造 SyncBuffData 并广播 RPC。</summary>
    private void BroadcastBuffChanged(string targetName, ushort buffTypeId, ushort stackCount, bool added) {
        var pawn = FindPawnByName(targetName);
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
