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
    [SyncVarFlags(SyncFlags.Interpolated)]
    public SyncVar<Vector2> Position;

    /// <summary>单位朝向方向向量（XZ 平面单位向量）。</summary>
    [SyncVarFlags(SyncFlags.Interpolated)]
    public SyncVar<Vector2> Direction;

    /// <summary>单位碰撞半径（技能范围判定用）。</summary>
    public SyncVar<float> BodyRadius;

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

    /// <summary>技能个体冷却列表（服务端权威回写）。</summary>
    public readonly SyncList<SyncSkillCooldown> SkillCooldowns = [];

    /// <summary>当前施法技能 ID（0=无施法）。</summary>
    public SyncVar<ushort> SkillCasting;

    /// <summary>当前施法剩余读条时间（秒）。</summary>
    public SyncVar<float> SkillCastRemaining;

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

#pragma warning disable CS0067 // 状态事件由 Server 桥接投影触发（跨程序集订阅，实体内仅声明）
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
#pragma warning restore CS0067

    /// <summary>技能施放请求事件。</summary>
    public event Action<UnitPawn, SyncSkillRequest>? SkillCastRequested;

    /// <summary>玩家输入处理回调。参数：实体、输入包、帧间隔。</summary>
    public Action<UnitPawn, UnitInputPacket, float>? InputHandler {
        get;
        set;
    }

    /// <summary>当前移动方向（由控制器逐逻辑帧注入，纯本地变量，不参与网络同步）。</summary>
    private Vector2 _moveDir;

    /// <summary>
    /// 确定性移动管线（由 Server/Client 装配时注入 <c>Logic.Movement.MovementResolver.Move</c>）。
    /// 输入：位置、方向、速度、帧间隔 → 输出：场景交互后的最终位置。
    /// 移动规则与场景交互统一在 Logic 层，本实体只做状态落点。
    /// </summary>
    public Func<Vector2, Vector2, float, float, Vector2>? MoveResolver;

    /// <summary>
    /// 设置当前移动方向。由 <see cref="UnitController"/> 在客户端预测与服务端权威阶段
    /// 都调用，驱动 <see cref="Update"/> 执行确定性位移。
    /// </summary>
    /// <param name="moveDir">移动方向向量（无需单位化）。</param>
    public void SetMovementInput(Vector2 moveDir) {
        _moveDir = moveDir;
    }

    /// <summary>
    /// 确定性移动结算：客户端预测与服务端权威都执行。
    /// 客户端本地立即反馈（消除 RTT 卡顿），服务端为权威位置，LES 回滚重放自动纠偏。
    /// 位移计算委托给 Logic 层移动管线（<see cref="MoveResolver"/>），本方法只写 SyncVar 状态。
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
    /// 服务端调用：接收控制器转发的玩家输入。仅调用 <see cref="InputHandler"/> 委托，
    /// 移动逻辑由 Logic 层消费（与 SkillCastRequested → Server → Logic 转发模式一致）。
    /// </summary>
    /// <param name="input">玩家输入包。</param>
    /// <param name="deltaTime">距上一逻辑帧的间隔时间（秒）。</param>
    public void ServerApplyInput(UnitInputPacket input, float deltaTime) {
        if (!IsServer)
            return;

        InputHandler?.Invoke(this, input, deltaTime);
    }
}
