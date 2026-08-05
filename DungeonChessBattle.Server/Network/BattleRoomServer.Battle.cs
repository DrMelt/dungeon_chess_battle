using System.Numerics;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// BattleRoomServer 的单位实体创建、战斗管理与 RPC 处理。
/// </summary>
public partial class BattleRoomServer {
    /// <summary>
    /// 在本房间的 SEM 中创建 UnitPawn 实体。
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

        // 订阅该 Pawn 的技能 RPC 事件
        entity.SkillCastRequested += OnPawnSkillCast;

        _roomPawns.Add(entity);

        // Logic 层创建单位
        _logicService.CreateUnit(RoomId, unitName, camp);

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
    public BattleManager StartBattle() {
        if (_battle != null && _battle.CurrentPhase == BattlePhase.Running)
            return _battle;

        _battle = _logicService.StartBattleInRoom(RoomId);
        _battle.BattleStarted += OnBattleStarted;
        _battle.BattleEnded += OnBattleEnded;
        return _battle;
    }

    /// <summary>
    /// 获取本房间的 BattleRoomEntity。
    /// </summary>
    public BattleRoomEntity? GetRoomEntity() => _roomEntity;

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
    /// 处理通过 UnitPawn 实例事件到达的技能施放请求。
    /// </summary>
    private void OnPawnSkillCast(UnitPawn casterPawn, SyncSkillRequest req) {
        var targetPawn = FindPawnById(req.TargetUnitNetId);
        if (targetPawn == null) {
            _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC: target pawn {TargetId} not found.", RoomId, req.TargetUnitNetId);
            return;
        }

        var casterModel = _logicService.FindUnitModel(casterPawn.UnitName.Value);
        var targetModel = _logicService.FindUnitModel(targetPawn.UnitName.Value);
        if (casterModel == null || targetModel == null) {
            _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC: unit model not found in Logic layer.", RoomId);
            return;
        }

        if (_battle == null) {
            _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC: no active battle.", RoomId);
            return;
        }

        float oldTargetHealth = targetModel.Health;

        if (req.IsDamage) {
            var skill = new SkillDamageModel {
                Damage = req.DamageOrCureValue,
                DamageType = (DamageType)req.DamageType
            };
            GameLogicService.CastSkill(_battle, casterModel, targetModel, skill);
        }
        else {
            var skill = new SkillCureModel { CurePotency = -req.DamageOrCureValue };
            GameLogicService.CastSkill(_battle, casterModel, targetModel, skill);
        }

        targetPawn.ServerSetHealth(targetModel.Health);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Skill result: {Caster} -> {Target}, HP: {OldHealth:F0} -> {NewHealth:F0}",
                RoomId, casterPawn.UnitName.Value, targetPawn.UnitName.Value, oldTargetHealth, targetPawn.Health.Value);
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
                _roomEntity.WinnerCamp.Value = BattleResolver.HasAliveUnits(gameRoom.UnitsA)
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
            GameLogicService.EndBattle(_battle);
        }
    }
}
