using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Combat.Hates;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Events;
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
    /// 房间线程首帧初始化：创建根实体、订阅实体事件、创建战斗世界、
    /// 从 Store 迁移准备期单位。此后 EntityManager 不再被其他线程触碰。
    /// </summary>
    private void InitializeFromStore() {
        // 在房间 SEM 中创建 BattleRoomEntity，并绑定为战斗世界的房间状态载体；
        // 此后阶段状态经 IBattleRoom 直接读写，无需事件翻译。
        var roomEntity = EntityManager.AddEntity<BattleRoomEntity>(e => {
            e.RoomId.Value = RoomId;
            // 注入服务端权威副本键，客户端据此加载对应的环境场景
            e.DungeonKey.Value = _dungeonKey;
        }) ?? throw new InvalidOperationException($"Failed to create BattleRoomEntity for room '{RoomId}'.");
        _battleScene.BindRoom(roomEntity);

        // 从 Store 迁移准备期单位；同阵营按序错开出生点，避免重名/同阵营单位重叠
        var units = _stateStore.GetPrepareUnits(RoomId);
        int campAIndex = 0, campBIndex = 0;
        foreach (var selection in units) {
            // 玩家单位首个阵营为主阵营，作为出生点分边依据
            var spawnPos = selection.Camps[0] == CampConstants.CampA
                ? new Vector2(campAIndex++ * SpawnSpacing, 0)
                : new Vector2(5f + campBIndex++ * SpawnSpacing, 0);
            var pawn = CreatePawnEntity(selection.UnitName, selection.Camps, spawnPos);
            _pawnByPlayerId[selection.PlayerId] = pawn;
        }

        // 按房间选中的副本配置生成敌人（Camp_BOSS 阵营，服务端 AI 驱动）
        SpawnDungeonEnemies();

        // 战斗循环收编进 LES tick 生命周期：Update=ApplyDecisions 先于位移，
        // LateUpdate=Tick 在实体更新后、状态包发送前。
        // 此后房间线程每逻辑 tick 自动驱动 BattleLoop 与战斗世界，
        // 与实体同步严格 1:1，时间由 LES accumulator 统一管理。
        EntityManager.AddLocalSingleton(new BattleLoop(_battleScene, HandleDomainEvent));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Initialized from store: {UnitCount} units migrated.",
                RoomId, units.Count);
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
                var pawn = CreatePawnEntity(config.ConfigKey, config.Camps, spawnPos);
                // 注入智能决策器，战斗世界按 IBattleUnit.Intelligence 识别并驱动该单位
                pawn.Intelligence = config.Intelligence;
            }
        }
    }

    /// <summary>
    /// 在本房间的 SEM 中创建 UnitPawn 实体。仅房间线程调用。
    /// </summary>
    public UnitPawn CreatePawnEntity(string unitName, IReadOnlyList<string> camps, Vector2 spawnPos) {
        // 兜底防御，上游网络入口已校验，这里仅防未来新增路径绕过校验
        if (unitName.Length > EntityConstants.MaxUnitNameLength)
            unitName = unitName[..EntityConstants.MaxUnitNameLength];
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

        // 从单位配置注入 Pawn 战斗系数，权威由战斗世界直接读写 IBattleUnit 载体
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
            entity.HateFactor = config.HateFactor;
            entity.HateRule = config.HateRule ?? DefaultHateRule.Instance;
        }

        // 注册到战斗世界，读条、冷却与 Buff 由 Tick 写回 Pawn
        _battleScene.AddUnit(entity);

        // 注入碰撞半径与移动管线，Logic 层 MovementResolver，含场景交互。
        // 场景两端口径一致，从同一副本布局构建 Aether 世界，保证预测与权威确定性一致；
        // 空间演员注册已在 AddUnit 收敛，位置与半径由提供器延迟读取实体 SyncVar。
        entity.MoveResolver = (pos, dir, speed, dt) =>
            MovementResolver.Move(pos, dir, speed, dt, entity.BodyRadius.Value, _battleScene.MovementScene, entity.Id);

        return entity;
    }

    /// <summary>
    /// 在本房间范围内按 NetId 查找 UnitPawn，不再跨房间查找。
    /// </summary>
    public UnitPawn? FindPawnById(ushort netId) {
        return _roomPawns.Find(p => p.Id == netId);
    }

    /// <summary>
    /// 在本房间启动战斗：委托战斗世界阶段机；阶段状态由战斗世界经 IBattleRoom 直接写入载体。
    /// </summary>
    public void StartBattle() {
        _battleScene.StartBattle();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomId: {RoomId}] Battle started, phase={Phase}", RoomId, _battleScene.CurrentPhase);
    }

    /// <summary>
    /// 处理通过 UnitPawn 实例事件到达的玩家输入。
    /// 移动已由 UnitPawn.Update() 确定性结算，客户端预测加服务端权威，
    /// 本方法仅作服务端输入钩子，保留日志与未来扩展。
    /// </summary>
    private void OnPawnInput(UnitPawn pawn, UnitInputPacket input, float deltaTime) {
        // 移动已由 UnitPawn.Update() 确定性结算，客户端预测加服务端权威。
        // 此处驱动移动即打断读条，战斗世界保留既有行为。
        _battleScene.OnUnitMoved(pawn, input.MoveDirection);

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
        if (_battleScene.CurrentPhase != BattlePhase.Running) {
            _logger.LogWarning("[RoomId: {RoomId}] Skill request dropped: battle not running.", RoomId);
            return false;
        }

        // 发起读条：战斗世界面向 IBattleUnit 校验冷却并写入读条状态
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

        bool began = _battleScene.BeginCast(casterPawn, new SkillKeyId(req.SkillTypeId), target, targetPos);
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
    /// 领域事件 → 网络翻译：把战斗世界产出的 IDomainEvent 转换为 RPC / SyncVar 写回。
    /// Health、读条、冷却与 Buff 列表已由战斗世界直接写 Pawn SyncVar，
    /// 房间级阶段状态已由战斗世界经 IBattleRoom 直接写入载体，
    /// 此处仅投影瞬时表现与实体级状态写回：受击 RPC、死亡状态与聚焦清理、Buff 增减 RPC。
    /// </summary>
    private void HandleDomainEvent(IDomainEvent domainEvent) {
        switch (domainEvent) {
            case DamageOccurred dmg:
                FindPawnById(dmg.TargetNetId)?
                    .BroadcastDamageTaken(dmg.AppliedDamage, dmg.DamageType);
                break;

            case UnitDied died:
                var deadPawn = FindPawnById(died.UnitNetId);
                deadPawn?.SetMovementInput(Vector2.Zero);
                deadPawn?.UnitState.Value = 1;
                ClearFocusTargetsTo(died.UnitNetId);
                break;

            case BuffApplied buff:
                BroadcastBuffChanged(buff.TargetNetId, buff.BuffTypeId, (ushort)buff.StackCount, added: true);
                break;

            case BuffExpired buffExp:
                BroadcastBuffChanged(buffExp.TargetNetId, buffExp.BuffTypeId, 0, added: false);
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
