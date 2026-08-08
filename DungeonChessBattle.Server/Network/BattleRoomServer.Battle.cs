using System.Numerics;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.GameConfig;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

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

        // 创建 Logic 层房间
        _logicService.CreateRoom(RoomId);

        // 注入技能解析器：按技能配置 ID 构造运行时技能模型（服务端持有配置表）
        _logicService.SetSkillResolver(skillId => {
            var config = GameConfigDB.GetSkillById(skillId);
            return config != null ? GameConfigDB.ToSkillModel(config) : null;
        });

        // 注入服务端权威创建时间（需在 CreateRoom 之后：GetRoom 依赖房间已登记；
        // 且不能在 AddEntity initAction 中注入——OnConstructed 会以默认值覆盖运行时值）
        var gameRoom = _logicService.GetRoom(RoomId);
        if (gameRoom != null && _roomEntity != null) {
            _roomEntity.CreatedUnixTime.Value =
                new DateTimeOffset(gameRoom.CreatedAt).ToUnixTimeSeconds();
        }

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
        var model = _logicService.CreateUnit(RoomId, unitName, camp);
        var configEntry = UnitRegistry.Instance.GetByDisplayName(unitName);
        if (configEntry != null)
            model.CopyStatsFrom(GameConfigDB.ToUnitModel(configEntry.Config));
        model.Position = new Vector3(spawnPos.X, 0f, spawnPos.Y);

        return entity;
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

        _battle = _logicService.StartBattleInRoom(RoomId);
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
    /// 处理通过 UnitPawn 实例事件到达的玩家输入：转发到 Logic 层结算移动，
    /// 并将 Logic 层权威位置回写 Pawn（LES SyncVar），使客户端渲染可见。
    /// Logic 层为移动权威；Pawn 位置 SyncVar 仅作网络同步载体。
    /// </summary>
    private void OnPawnInput(UnitPawn pawn, UnitInputPacket input, float deltaTime) {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomServer:{RoomId}] PawnInput: {Unit} dir={Dir}, dt={Dt}",
                RoomId, pawn.UnitName.Value, input.MoveDirection, deltaTime);
        _logicService.UpdatePlayerMovement(RoomId, pawn.UnitName.Value, input.MoveDirection, deltaTime);

        // Logic 层结算后回写 Pawn 位置（客户端渲染读 pawn.Position.Value）
        var model = _logicService.FindUnitModel(RoomId, pawn.UnitName.Value);
        if (model is not null) {
            var pos = model.Position;
            pawn.Position.Value = new Vector2(pos.X, pos.Z);
        }
        else {
            _logger.LogWarning("[RoomServer:{RoomId}] Unit model not found for input handling: {Unit}", RoomId, pawn.UnitName.Value);
        }
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

        bool began = _logicService.BeginSpell(RoomId, casterPawn.UnitName.Value, req.SkillTypeId, targetName, targetPos);
        if (!began) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] Skill cast rejected (cooldown): {Caster}, SkillId={SkillId}",
                    RoomId, casterPawn.UnitName.Value, req.SkillTypeId);
            return;
        }

        // 回写 Pawn 施法状态（客户端渲染读 pawn.SkillCasting / SkillCastRemaining）
        var casterModel = _logicService.FindUnitModel(RoomId, casterPawn.UnitName.Value);
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

            var gameRoom = _logicService.GetRoom(RoomId);
            if (gameRoom != null && _logicService.CheckBattleEnded(gameRoom)) {
                _roomEntity.WinnerCamp.Value =
                    gameRoom.Units.Any(u => u.Health > 0 && u.Camps.Contains(CampConstants.CampA))
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

        var gameRoom = _logicService.GetRoom(RoomId);
        if (gameRoom != null && _logicService.CheckBattleEnded(gameRoom)) {
            _logicService.EndBattle(_battle);
        }
    }
}
