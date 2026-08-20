using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
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

    /// <summary>
    /// 初始化单位 Pawn 实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public UnitPawn(EntityParams entityParams) : base(entityParams) { }

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

    /// <summary>单位所属阵营列表，装配期一次写入的权威同步数据。</summary>
    public readonly SyncSpanSerializable<SyncCampsData> CampsData = new(() => new SyncCampsData());

    /// <summary>阵营列表只读投影，服务端与客户端同源直读；装配期一次写入后不变，每次读取新建数组。</summary>
    public IReadOnlyList<string> CampTags => CampsData.Value.ToArray();

    /// <summary>单位状态，0 表示存活，1 表示死亡。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<byte> UnitState;

    /// <summary>全局冷却截止的服务器逻辑 tick，客户端据此本地推算剩余时间。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<ushort> GcdEndServerTick;

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

    /// <summary>AI 动作执行器，AddUnit 时由 BattleScene 注入；客户端或未绑定实例为空，RunAI 不动作。</summary>
    private IAiExecutor? _aiExecutor;

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

    /// <summary>聚焦目标变化事件，客户端同步阶段触发。参数：实体、目标单位网络 ID。</summary>
    public event Action<UnitPawn, ushort>? FocusTargetChanged;

    /// <summary>玩家输入处理回调。参数：实体、输入包、帧间隔。</summary>
    public Action<UnitPawn, UnitInputPacket, float>? InputHandler {
        get;
        set;
    }

    /// <summary>服务端权威战斗状态，不参与网络同步；客户端实例保留空状态不推进。</summary>
    public UnitCombatState RuntimeState { get; } = new();

    /// <summary>当前移动方向，由控制器逐逻辑帧注入，纯本地变量，不参与网络同步。</summary>
    private Vector2 _moveInput;

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
    /// <param name="moveInput">移动方向向量，无需单位化。</param>
    public void SetMovementInput(Vector2 moveInput) {
        _moveInput = moveInput;
    }

    /// <summary>
    /// 确定性移动结算：客户端预测与服务端权威都执行。
    /// 客户端本地立即反馈，消除 RTT 卡顿，服务端为权威位置，LES 回滚重放自动纠偏。
    /// 位移计算委托给 Logic 层移动管线 <see cref="MoveResolver"/>，本方法只写 SyncVar 状态。
    /// </summary>
    protected override void Update() {
        base.Update();
        if (MoveResolver == null || _moveInput.LengthSquared() <= 0.0001f || BaseSpeed.Value <= 0f)
            return;

        Position.Value = MoveResolver(Position.Value, _moveInput, BaseSpeed.Value, EntityManager.DeltaTimeF);

        var dir = Vector2.Normalize(_moveInput);
        if (Direction.Value != dir)
            Direction.Value = dir;
    }


    /// <summary>
    /// 注册 RPC 动作：服务端到客户端事件广播。
    /// </summary>
    /// <param name="r">RPC 注册器。</param>
    protected override void RegisterRPC(ref RPCRegistrator r) {
        base.RegisterRPC(ref r);
        // 客户端在同步阶段检测聚焦目标变化
        r.BindOnChange<UnitPawn, ushort>(ref FocusTargetNetId, (e, t) => e.OnFocusTargetChangedBySync(t), BindOnChangeFlags.ExecuteOnSync);

        // 客户端在同步阶段检测血量与死亡状态变化
        r.BindOnChange<UnitPawn, float>(ref Health, (e, h) => e.OnHealthChangedBySync(h), BindOnChangeFlags.ExecuteOnSync);
        r.BindOnChange<UnitPawn, byte>(ref UnitState, (e, s) => e.OnUnitStateChangedBySync(s), BindOnChangeFlags.ExecuteOnSync);
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
