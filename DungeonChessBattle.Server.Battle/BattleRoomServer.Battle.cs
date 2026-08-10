using System.Numerics;
using DungeonChessBattle.Core;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Logic.Movement;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// BattleRoomServer 的初始化（从 Store 自取数据）、单位实体创建、战斗管理与 RPC 处理。
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

        // Logic 层单房间门面已在构造时持有本房间；此处仅注入运行时依赖
        // 注入技能解析器：按技能配置 ID 构造运行时技能模型（服务端持有配置表）
        _logicService.SetSkillResolver(skillId => {
            var config = GameConfigDB.GetSkillById(skillId);
            return config != null ? GameConfigDB.ToSkillModel(config) : null;
        });

        // 注入服务端权威创建时间（Logic 房间在构造时创建，此处直接取用；
        // 且不能在 AddEntity initAction 中注入——OnConstructed 会以默认值覆盖运行时值）
        var gameRoom = _logicService.Room;
        _roomEntity?.CreatedUnixTime.Value =
                new DateTimeOffset(gameRoom.CreatedAt).ToUnixTimeSeconds();

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
        // 兜底防御（上游网络入口已校验，这里仅防未来新增路径绕过校验）
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

        // Logic 层创建单位：先从注册表取配置建模并注入运行时数值（BaseSpeed 等），
        // 避免裸 UnitModel 速度恒为 0；再同步初始位置（与 Pawn 一致，避免回写时拉回原点）
        var model = _logicService.CreateUnit(unitName, camp);
        var configEntry = UnitRegistry.Instance.GetByDisplayName(unitName);
        if (configEntry != null)
            model.CopyStatsFrom(GameConfigDB.ToUnitModel(configEntry.Config));
        model.Position = new Vector3(spawnPos.X, 0f, spawnPos.Y);

        // 订阅 Logic 权威模型事件，桥接为客户端事件广播（受击 / Buff 增减）
        if (model is UnitModel unitModel) {
            unitModel.TookDamage += OnModelTookDamage;
            unitModel.BuffAdded += OnModelBuffAdded;
            unitModel.BuffRemoved += OnModelBuffRemoved;
        }

        // 注入 Pawn 移动速度：预测位移依赖 BaseSpeed，须在模型 CopyStatsFrom 之后回填
        entity.BaseSpeed.Value = model.MoveSpeed;

        // 注入碰撞半径与移动管线（Logic 层 MovementResolver，含场景交互）。
        // 场景两端口径一致（OpenMovementScene 无碰撞），保证预测与权威确定性一致。
        entity.BodyRadius.Value = model.BodyRadius;
        entity.MoveResolver = (pos, dir, speed, dt) =>
            MovementResolver.Move(pos, dir, speed, dt, entity.BodyRadius.Value, OpenMovementScene.Instance);

        return entity;
    }

    /// <summary>Logic 权威模型受击事件：桥接为对应 Pawn 的客户端广播。</summary>
    private void OnModelTookDamage(UnitModel model, float damage, DamageType damageType) {
        var pawn = _roomPawns.Find(p => p.UnitName.Value == model.UnitStateName);
        pawn?.BroadcastDamageTaken(damage, damageType);
    }

    /// <summary>Logic 权威模型 Buff 添加事件：桥接为对应 Pawn 的客户端广播。</summary>
    private void OnModelBuffAdded(UnitModel model, IBuff buff) {
        var pawn = _roomPawns.Find(p => p.UnitName.Value == model.UnitStateName);
        pawn?.BroadcastBuffAdded(MapSingleModelBuff(buff));
    }

    /// <summary>Logic 权威模型 Buff 移除事件：桥接为对应 Pawn 的客户端广播。</summary>
    private void OnModelBuffRemoved(UnitModel model, IBuff buff) {
        var pawn = _roomPawns.Find(p => p.UnitName.Value == model.UnitStateName);
        pawn?.BroadcastBuffRemoved(MapSingleModelBuff(buff));
    }

    /// <summary>将单条 Logic Buff 模型映射为同步 Buff 数据快照（与 MapModelBuffs 同源转换）。</summary>
    private static SyncBuffData MapSingleModelBuff(IBuff buff) {
        if (buff is not BuffModel model)
            return default;

        var data = new SyncBuffData {
            BuffTypeId = model.BuffTypeId,
            RemainingDuration = (float)model.Duration,
            StackCount = (ushort)model.Superpositions,
            MaxStackCount = (ushort)model.MaxSuperpositions,
        };

        switch (model) {
            case BuffDOTModel dot:
                data.TickInterval = 1f;
                data.TickValue = dot.DamagePerSec;
                data.DamageType = (byte)dot.DamageType;
                break;
            case BuffHOTModel hot:
                data.TickInterval = 1f;
                data.TickValue = -hot.HealthPerSec; // 负值表示治疗
                break;
        }

        return data;
    }

    /// <summary>
    /// 在本房间范围内按 NetId 查找 UnitPawn，不再跨房间查找。
    /// </summary>
    public UnitPawn? FindPawnById(ushort netId) {
        return _roomPawns.Find(p => p.Id == netId);
    }

    /// <summary>
    /// 在本房间启动战斗。
    /// </summary>
    public void StartBattle() {
        if (_battle != null && _battle.CurrentPhase == BattlePhase.Running)
            return;

        _battle = _logicService.StartBattle();
        _battle.BattleStarted += OnBattleStarted;
        _battle.BattleEnded += OnBattleEnded;
    }

    /// <summary>
    /// RPC：客户端请求创建单位。
    /// </summary>
    private void OnCreateUnitRequested(BattleRoomEntity roomEntity, SyncCreateUnitRequest req) {
        // 防御：RPC 数据来自网络，必须校验（Put(str,max) 超限会静默变空串，必须显式拦截）
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
    /// 移动已由 UnitPawn.Update() 确定性结算（客户端预测 + 服务端权威），
    /// 本方法仅作服务端输入钩子（保留日志与未来扩展）。
    /// </summary>
    private void OnPawnInput(UnitPawn pawn, UnitInputPacket input, float deltaTime) {
        // 移动已由 UnitPawn.Update() 确定性结算（客户端预测 + 服务端权威），
        // 此处仅作服务端输入钩子（保留日志与未来扩展），不再做移动结算。
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomServer:{RoomId}] PawnInput: {Unit} dir={Dir}, dt={Dt}",
                RoomId, pawn.UnitName.Value, input.MoveDirection, deltaTime);
    }

    /// <summary>
    /// 处理通过 UnitPawn 实例事件到达的技能施放请求：由瞬发结算改为发起读条。
    /// 服务端设置施法状态（SkillCasting + SkillCastRemaining），读条由房间 tick 推进，
    /// 完成时 Logic 结算并回写。
    /// </summary>
    private void OnPawnSkillCast(UnitPawn casterPawn, SyncSkillRequest req) {
        if (_battle == null || _battle.CurrentPhase != BattlePhase.Running) {
            _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC dropped: battle not running.", RoomId);
            return;
        }

        // 发起读条：Logic 层按 SkillTypeId 解析技能时长并暂存目标（冷却校验在 BeginSpell 内）
        string? targetName = null;
        Vector3? targetPos = null;
        if (req.TargetUnitNetId != 0) {
            var targetPawn = FindPawnById(req.TargetUnitNetId);
            if (targetPawn == null) {
                _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC: target pawn {TargetId} not found.",
                    RoomId, req.TargetUnitNetId);
                return;
            }
            targetName = targetPawn.UnitName.Value;
        }
        else {
            // 位置目标技能（范围伤害）：XZ 平面
            targetPos = new Vector3(req.TargetPosX, 0f, req.TargetPosZ);
        }

        bool began = _logicService.BeginSpell(casterPawn.UnitName.Value, req.SkillTypeId, targetName, targetPos);
        if (!began) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] Skill cast rejected (cooldown): {Caster}, SkillId={SkillId}",
                    RoomId, casterPawn.UnitName.Value, req.SkillTypeId);
            return;
        }

        // 回写 Pawn 施法状态（客户端渲染读 pawn.SkillCasting / SkillCastRemaining）
        var casterModel = _logicService.FindUnitModel(casterPawn.UnitName.Value);
        if (casterModel is UnitModel model) {
            casterPawn.SkillCasting.Value = model.SpellingSkillId;
            casterPawn.SkillCastRemaining.Value = Math.Max(0f, model.SpellRemaining);
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Skill cast began: {Caster} -> {Target}, SkillId={SkillId}",
                RoomId, casterPawn.UnitName.Value, targetName ?? "(position)", req.SkillTypeId);
    }

    /// <summary>
    /// 战斗开始时更新房间实体阶段状态。
    /// </summary>
    private void OnBattleStarted() {
        if (_roomEntity != null) {
            _roomEntity.BattlePhase.Value = (byte)BattlePhase.Running;
            _roomEntity.IsFinished.Value = false;
        }
    }

    /// <summary>
    /// 战斗结束时更新房间实体阶段与胜方阵营状态。
    /// </summary>
    private void OnBattleEnded() {
        if (_roomEntity != null) {
            _roomEntity.BattlePhase.Value = (byte)BattlePhase.Finished;
            _roomEntity.IsFinished.Value = true;

            if (_logicService.CheckBattleEnded()) {
                _roomEntity.WinnerCamp.Value =
                    _logicService.GetUnits().Any(u => u.Health > 0 && u.Camps.Contains(CampConstants.CampA))
                        ? CampConstants.CampA
                        : CampConstants.CampB;
            }
        }
    }

    /// <summary>
    /// 检查战斗是否已结束，结束时通知战斗管理器。
    /// </summary>
    private void CheckBattleEnded() {
        if (_battle?.CurrentPhase != BattlePhase.Running)
            return;

        if (_logicService.CheckBattleEnded()) {
            _logicService.EndBattle(_battle);
        }
    }
}
