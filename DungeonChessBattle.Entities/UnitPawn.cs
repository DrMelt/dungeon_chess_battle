using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DamageType = DungeonChessBattle.Battle.Domain.Combat.DamageType;
using LiteEntitySystem;
using LiteEntitySystem.Extensions;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Battle.Domain.Combat.Hates;
using DungeonChessBattle.Battle.Domain.Intelligence;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 实时化的单位 Pawn 实体。继承 PawnLogic，支持移动、技能、预测回滚。
/// 逐步替代 UnitSyncEntity，回合制纯数据载体。
/// </summary>
public partial class UnitPawn : PawnLogic {
    private static RemoteCallSerializable<SyncDamageData> DamageTakenRPC;
    private static RemoteCallSerializable<SyncBuffData> BuffAddedRPC;
    private static RemoteCallSerializable<SyncBuffData> BuffRemovedRPC;

    /// <summary>单位名称。</summary>
    public readonly SyncString UnitName = new();

    /// <summary>单位位置，XZ 平面。</summary>
    [SyncVarFlags(SyncFlags.Interpolated)]
    public SyncVar<Vector2> Position;

    /// <summary>单位朝向方向向量，XZ 平面单位向量。</summary>
    [SyncVarFlags(SyncFlags.Interpolated)]
    public SyncVar<Vector2> Direction;

    /// <summary>单位碰撞半径，供技能范围判定使用。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> BodyRadius;

    /// <summary>当前生命值。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> Health;

    /// <summary>最大生命值。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> MaxHealth;

    /// <summary>阵营字符串标识，如 "Camp_A"、"Camp_B"。</summary>
    public readonly SyncString Camp = new();

    /// <summary>单位状态，0 表示存活，1 表示死亡。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<byte> UnitState;

    /// <summary>剩余全局冷却时间，秒。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> GcdRemaining;

    /// <summary>技能个体冷却列表，服务端权威回写。</summary>
    public readonly SyncList<SyncSkillCooldown> SkillCooldowns = [];

    /// <summary>当前施法技能 ID，0 表示无施法。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<ushort> SkillCasting;

    /// <summary>当前施法剩余读条时间，秒。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> SkillCastRemaining;

    /// <summary>物理攻击基础系数即伤害倍率。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> PhysicalAttackBase;

    /// <summary>魔法攻击基础系数即伤害倍率。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> MagicAttackBase;

    /// <summary>物理伤害承受系数即减免倍率。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> PhysicalTakePercent;

    /// <summary>魔法伤害承受系数即减免倍率。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> MagicTakePercent;

    /// <summary>治疗强度系数即治疗倍率。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> CureIntensity;

    /// <summary>基础移动速度。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<float> BaseSpeed;

    /// <summary>单位当前持有的 Buff 列表。</summary>
    public readonly SyncList<SyncBuffData> BuffsList = [];

    /// <summary>单位拥有的技能定义列表，引用共享单位配置，装配期写入后只读，不参与网络同步。</summary>
    public IReadOnlyList<SkillDefinition> Skills {
        get; set;
    } = [];

    /// <summary>单位智能决策器，装配期注入后只读，不参与网络同步；null 表示由外部输入驱动（玩家单位）。</summary>
    public IUnitIntelligence? Intelligence {
        get; set;
    }

    /// <summary>仇恨生成倍率，引用单位配置，装配期写入后只读，不参与网络同步。</summary>
    public float HateFactor {
        get; set;
    } = 1f;

    /// <summary>仇恨规则，按单位配置注入，装配期写入后只读，不参与网络同步。</summary>
    public IHateRule HateRule {
        get; set;
    } = DefaultHateRule.Instance;

    /// <summary>单位仇恨列表。</summary>
    public readonly SyncList<SyncHateData> HatesList = [];

    /// <summary>聚焦目标单位网络 ID，0 表示无聚焦目标。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<ushort> FocusTargetNetId;

    /// <summary>生命值变化事件。参数：实体、新生命值、旧生命值。</summary>
    public event Action<UnitPawn, float, float>? HealthChanged;

    /// <summary>单位死亡事件。</summary>
    public event Action<UnitPawn>? UnitDied;

    /// <summary>单位受到伤害事件。参数：实体、实际伤害量、伤害类型。</summary>
    public event Action<UnitPawn, float, DamageType>? TookDamage;

    /// <summary>添加 Buff 事件。</summary>
    public event Action<UnitPawn, SyncBuffData>? BuffAdded;

    /// <summary>移除 Buff 事件。</summary>
    public event Action<UnitPawn, SyncBuffData>? BuffRemoved;

    /// <summary>聚焦目标变化事件，客户端同步阶段触发。参数：实体、目标单位网络 ID。</summary>
    public event Action<UnitPawn, ushort>? FocusTargetChanged;

    /// <summary>玩家输入处理回调。参数：实体、输入包、帧间隔。</summary>
    public Action<UnitPawn, UnitInputPacket, float>? InputHandler {
        get;
        set;
    }

    /// <summary>当前移动方向，由控制器逐逻辑帧注入，纯本地变量，不参与网络同步。</summary>
    private Vector2 _moveDir;

    /// <summary>客户端同步阶段缓存的上一次生命值，用于 HealthChanged 的 oldHealth。</summary>
    private float _lastHealth;

    /// <summary>
    /// 确定性移动管线，由 Server 与 Client 装配时注入 <c>Logic.Movement.MovementResolver.Move</c>。
    /// 输入：位置、方向、速度、帧间隔 → 输出：场景交互后的最终位置。
    /// 移动规则与场景交互统一在 Logic 层，本实体只做状态落点。
    /// </summary>
    public Func<Vector2, Vector2, float, float, Vector2>? MoveResolver;

    /// <summary>
    /// 设置当前移动方向。由 <see cref="UnitController"/> 在客户端预测与服务端权威阶段
    /// 都调用，驱动 <see cref="Update"/> 执行确定性位移。
    /// </summary>
    /// <param name="moveDir">移动方向向量，无需单位化。</param>
    public void SetMovementInput(Vector2 moveDir) {
        _moveDir = moveDir;
    }

    /// <summary>
    /// 确定性移动结算：客户端预测与服务端权威都执行。
    /// 客户端本地立即反馈，消除 RTT 卡顿，服务端为权威位置，LES 回滚重放自动纠偏。
    /// 位移计算委托给 Logic 层移动管线 <see cref="MoveResolver"/>，本方法只写 SyncVar 状态。
    /// </summary>
    protected override void Update() {
        base.Update();
        if (MoveResolver == null || _moveDir.LengthSquared() <= 0.0001f || BaseSpeed.Value <= 0f)
            return;

        Position.Value = MoveResolver(Position.Value, _moveDir, BaseSpeed.Value, EntityManager.DeltaTimeF);

        var dir = _moveDir / _moveDir.Length(); // 已判非零，防除零
        if (Direction.Value != dir)
            Direction.Value = dir;
    }

    /// <summary>
    /// 初始化单位 Pawn 实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public UnitPawn(EntityParams entityParams) : base(entityParams) { }

    /// <summary>
    /// 注册 RPC 动作：服务端到客户端事件广播。
    /// </summary>
    /// <param name="r">RPC 注册器。</param>
    protected override void RegisterRPC(ref RPCRegistrator r) {
        base.RegisterRPC(ref r);
        // 服务端到客户端广播：受击与 Buff 增减事件，瞬时语义，携带完整数据
        r.CreateRPCAction<UnitPawn, SyncDamageData>(
            (e, d) => e.OnRpcDamageTaken(d),
            ref DamageTakenRPC,
            ExecuteFlags.SendToAll);
        r.CreateRPCAction<UnitPawn, SyncBuffData>(
            (e, b) => e.OnRpcBuffAdded(b),
            ref BuffAddedRPC,
            ExecuteFlags.SendToAll);
        r.CreateRPCAction<UnitPawn, SyncBuffData>(
            (e, b) => e.OnRpcBuffRemoved(b),
            ref BuffRemovedRPC,
            ExecuteFlags.SendToAll);

        // 客户端在同步阶段检测聚焦目标变化
        r.BindOnChange<UnitPawn, ushort>(ref FocusTargetNetId, (e, t) => e.OnFocusTargetChangedBySync(t), BindOnChangeFlags.ExecuteOnSync);

        // 客户端在同步阶段检测血量与死亡状态变化
        r.BindOnChange<UnitPawn, float>(ref Health, (e, h) => e.OnHealthChangedBySync(h), BindOnChangeFlags.ExecuteOnSync);
        r.BindOnChange<UnitPawn, byte>(ref UnitState, (e, s) => e.OnUnitStateChangedBySync(s), BindOnChangeFlags.ExecuteOnSync);
    }

    /// <summary>客户端接收：受击事件广播。</summary>
    private void OnRpcDamageTaken(SyncDamageData d) {
        TookDamage?.Invoke(this, d.Damage, (DamageType)d.DamageType);
    }

    /// <summary>客户端接收：Buff 添加事件广播。</summary>
    private void OnRpcBuffAdded(SyncBuffData buff) {
        BuffAdded?.Invoke(this, buff);
    }

    /// <summary>客户端接收：Buff 移除事件广播。</summary>
    private void OnRpcBuffRemoved(SyncBuffData buff) {
        BuffRemoved?.Invoke(this, buff);
    }

    /// <summary>客户端同步阶段：生命值变化，缓存旧值以提供 oldHealth。</summary>
    private void OnHealthChangedBySync(float newHealth) {
        var oldHealth = _lastHealth;
        _lastHealth = newHealth;
        HealthChanged?.Invoke(this, newHealth, oldHealth);
    }

    /// <summary>客户端同步阶段：单位状态变化，0 存活到 1 死亡。</summary>
    private void OnUnitStateChangedBySync(byte newState) {
        if (newState == 1)
            UnitDied?.Invoke(this);
    }

    /// <summary>客户端同步阶段：聚焦目标变化，0 表示无聚焦目标。</summary>
    private void OnFocusTargetChangedBySync(ushort targetNetId) {
        FocusTargetChanged?.Invoke(this, targetNetId);
    }

    /// <summary>服务端调用：广播受击事件到客户端。</summary>
    public void BroadcastDamageTaken(float damage, DamageType damageType) {
        ExecuteRPC(DamageTakenRPC, new SyncDamageData { Damage = damage, DamageType = (byte)damageType });
    }

    /// <summary>服务端调用：广播 Buff 添加事件到客户端。</summary>
    public void BroadcastBuffAdded(SyncBuffData buff) {
        ExecuteRPC(BuffAddedRPC, buff);
    }

    /// <summary>服务端调用：广播 Buff 移除事件到客户端。</summary>
    public void BroadcastBuffRemoved(SyncBuffData buff) {
        ExecuteRPC(BuffRemovedRPC, buff);
    }

    /// <summary>
    /// 服务端调用：接收控制器转发的玩家输入。仅调用 <see cref="InputHandler"/> 委托，
    /// 移动打断读条等消费在 Logic 层。
    /// </summary>
    /// <param name="input">玩家输入包。</param>
    /// <param name="deltaTime">距上一逻辑帧的间隔时间，秒。</param>
    public void ServerApplyInput(UnitInputPacket input, float deltaTime) {
        if (!IsServer)
            return;

        InputHandler?.Invoke(this, input, deltaTime);
    }
}
