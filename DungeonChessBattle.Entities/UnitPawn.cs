using System.Numerics;
using DungeonChessBattle.Core.Enums;
using LiteEntitySystem;
using LiteEntitySystem.Extensions;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 实时化的单位 Pawn 实体。继承 PawnLogic，支持移动、技能、预测回滚。
/// 逐步替代 UnitSyncEntity（回合制纯数据载体）。
/// </summary>
public class UnitPawn : PawnLogic {
    private static RemoteCallSerializable<SyncSkillRequest> CastSkillRPC;

    /// <summary>单位名称。</summary>
    public readonly SyncString UnitName = new();

    /// <summary>单位位置（XZ 平面）。</summary>
    public SyncVar<Vector2> Position;

    /// <summary>单位朝向角。</summary>
    public SyncVar<float> Rotation;

    /// <summary>当前生命值。</summary>
    public SyncVar<float> Health;

    /// <summary>最大生命值。</summary>
    public SyncVar<float> MaxHealth;

    /// <summary>阵营字符串标识（如 "Camp_A"、"Camp_B"）。</summary>
    public readonly SyncString Camp = new();

    /// <summary>单位状态（0=存活，1=死亡）。</summary>
    public SyncVar<byte> UnitState;

    /// <summary>剩余全局冷却时间（秒）。</summary>
    public SyncVar<float> GcdRemaining;

    /// <summary>物理攻击基础系数（伤害倍率）。</summary>
    public SyncVar<float> PhysicalAttackBase;

    /// <summary>魔法攻击基础系数（伤害倍率）。</summary>
    public SyncVar<float> MagicAttackBase;

    /// <summary>物理伤害承受系数（减免倍率）。</summary>
    public SyncVar<float> PhysicalTakePercent;

    /// <summary>魔法伤害承受系数（减免倍率）。</summary>
    public SyncVar<float> MagicTakePercent;

    /// <summary>治疗强度系数（治疗量倍率）。</summary>
    public SyncVar<float> CureIntensity;

    /// <summary>基础移动速度。</summary>
    public SyncVar<float> BaseSpeed;

    /// <summary>单位当前持有的 Buff 列表。</summary>
    public readonly SyncList<SyncBuffData> BuffsList = [];

    /// <summary>单位拥有的技能类型 ID 列表（对应配置表）。</summary>
    public readonly SyncList<ushort> SkillIds = [];

    /// <summary>单位仇恨列表。</summary>
    public readonly SyncList<SyncHateData> HatesList = [];

    /// <summary>生命值变化事件。参数：实体、新生命值、旧生命值。</summary>
    public event Action<UnitPawn, float, float>? HealthChanged;

    /// <summary>单位死亡事件。</summary>
    public event Action<UnitPawn>? UnitDied;

    /// <summary>添加 Buff 事件。</summary>
    public event Action<UnitPawn, SyncBuffData>? BuffAdded;

    /// <summary>移除 Buff 事件。</summary>
    public event Action<UnitPawn, SyncBuffData>? BuffRemoved;

    /// <summary>技能施放请求事件。</summary>
    public event Action<UnitPawn, SyncSkillRequest>? SkillCastRequested;

    /// <summary>
    /// 初始化单位 Pawn 实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public UnitPawn(EntityParams entityParams) : base(entityParams) { }

    /// <summary>
    /// 实体构造完成回调：初始化单位默认属性值。
    /// ⚠ LiteEntitySystem 1.2.2 语义：OnConstructed 在 AddEntity(initAction) 之后执行，
    /// 会覆盖服务端注入值。此处仅保留纯内部默认状态；
    /// 运行时注入字段（UnitName/Camp/Position 等）禁止在此赋默认值。
    /// </summary>
    protected override void OnConstructed() {
        Health.Value = 1000f;
        MaxHealth.Value = 1000f;
        UnitState.Value = 0;
        PhysicalAttackBase.Value = 1.0f;
        MagicAttackBase.Value = 1.0f;
        PhysicalTakePercent.Value = 1.0f;
        MagicTakePercent.Value = 1.0f;
        CureIntensity.Value = 1.0f;
        BaseSpeed.Value = 2.0f;
    }

    /// <summary>
    /// 注册 RPC 动作：技能施放请求（在服务端执行）。
    /// </summary>
    /// <param name="r">RPC 注册器。</param>
    protected override void RegisterRPC(ref RPCRegistrator r) {
        base.RegisterRPC(ref r);
        r.CreateRPCAction<UnitPawn, SyncSkillRequest>(
            (e, req) => e.OnRpcCastSkill(req),
            ref CastSkillRPC,
            ExecuteFlags.ExecuteOnServer);
    }

    private void OnRpcCastSkill(SyncSkillRequest req) {
        SkillCastRequested?.Invoke(this, req);
    }

    /// <summary>
    /// 客户端调用：请求施放技能。
    /// </summary>
    /// <param name="req">技能施放请求数据。</param>
    public void RequestCastSkill(SyncSkillRequest req) {
        ExecuteRPC(CastSkillRPC, req);
    }

    /// <summary>
    /// 服务端调用：设置生命值。生命值变化时触发事件，降至 0 时标记死亡。
    /// </summary>
    /// <param name="newHealth">新的生命值。</param>
    public void ServerSetHealth(float newHealth) {
        if (!IsServer)
            return;
        float oldHealth = Health.Value;
        Health.Value = Math.Clamp(newHealth, 0f, MaxHealth.Value);
        if (MathF.Abs(Health.Value - oldHealth) > 0.0001f) {
            HealthChanged?.Invoke(this, Health.Value, oldHealth);
            if (Health.Value <= 0) {
                UnitState.Value = 1;
                UnitDied?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// 服务端调用：添加一个 Buff。可叠加类型的同名 Buff 已存在时叠加层数。
    /// </summary>
    /// <param name="buffData">要添加的 Buff 数据。</param>
    public void ServerAddBuff(SyncBuffData buffData) {
        if (!IsServer)
            return;
        if (buffData.IsStackable) {
            for (int i = 0; i < BuffsList.Count; i++) {
                var existing = BuffsList[i];
                if (existing.BuffTypeId == buffData.BuffTypeId) {
                    existing.StackCount = (ushort)Math.Min(existing.StackCount + 1, existing.MaxStackCount);
                    existing.RemainingDuration = Math.Max(existing.RemainingDuration, buffData.RemainingDuration);
                    BuffsList[i] = existing;
                    return;
                }
            }
        }
        BuffsList.Add(buffData);
        BuffAdded?.Invoke(this, buffData);
    }

    /// <summary>
    /// 服务端调用：按索引移除一个 Buff。
    /// </summary>
    /// <param name="index">Buff 在列表中的索引。</param>
    public void ServerRemoveBuffAt(int index) {
        if (!IsServer)
            return;
        if (index < 0 || index >= BuffsList.Count)
            return;
        var removed = BuffsList[index];
        BuffsList.RemoveAt(index);
        BuffRemoved?.Invoke(this, removed);
    }

    /// <summary>
    /// 服务端调用：更新指定 Buff 的剩余持续时间。
    /// </summary>
    /// <param name="index">Buff 在列表中的索引。</param>
    /// <param name="newRemaining">新的剩余时间（秒）。</param>
    public void ServerUpdateBuffDuration(int index, float newRemaining) {
        if (!IsServer)
            return;
        if (index < 0 || index >= BuffsList.Count)
            return;
        var buff = BuffsList[index];
        buff.RemainingDuration = newRemaining;
        BuffsList[index] = buff;
    }

    /// <summary>
    /// 服务端调用：添加或累加对目标单位的仇恨值。
    /// </summary>
    /// <param name="targetUnitNetId">目标单位的 NetId。</param>
    /// <param name="hateValue">要累加的仇恨值。</param>
    public void ServerAddHate(ushort targetUnitNetId, float hateValue) {
        if (!IsServer)
            return;
        for (int i = 0; i < HatesList.Count; i++) {
            var existing = HatesList[i];
            if (existing.TargetUnitNetId == targetUnitNetId) {
                existing.HateValue += hateValue;
                HatesList[i] = existing;
                return;
            }
        }
        HatesList.Add(new SyncHateData { TargetUnitNetId = targetUnitNetId, HateValue = hateValue });
    }

    /// <summary>
    /// 按移动方向推进位置（移动向量超长时归一化）。
    /// </summary>
    /// <param name="moveDir">移动方向向量。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void ApplyMovement(Vector2 moveDir, float deltaTime) {
        if (moveDir.LengthSquared() > 1f)
            moveDir = Vector2.Normalize(moveDir);

        Position.Value += moveDir * BaseSpeed.Value * deltaTime;
    }

    /// <summary>
    /// 按帧递减剩余全局冷却时间。
    /// </summary>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public void UpdateCooldowns(float deltaTime) {
        if (GcdRemaining.Value > 0)
            GcdRemaining.Value = Math.Max(0, GcdRemaining.Value - deltaTime);
    }
}
