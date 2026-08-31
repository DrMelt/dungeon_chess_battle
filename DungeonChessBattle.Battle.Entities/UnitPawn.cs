using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using LiteEntitySystem;
using LiteEntitySystem.Extensions;
using DungeonChessBattle.Battle.Entities.SyncData;

namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// 网络投影载体。继承 PawnLogic，承载单位战斗状态的网络同步（SyncVar）。
/// 自身不做位移与结算：服务端经 <c>SyncFrom</c> 写入权威值，在线端经 <c>SyncInto</c> 回填领域单位。
/// 载体读数一律取 <c>Value</c>，本实体未标注插值同步标志。死亡状态不设字段，由 Health 派生。
/// </summary>
public partial class UnitPawn : PawnLogic {
    /// <summary>
    /// 初始化单位 Pawn 实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public UnitPawn(EntityParams entityParams) : base(entityParams) { }

    /// <summary>单位配置键，两端据此读取装配配置；不是显示名。</summary>
    public readonly SyncString UnitKeyName = new();

    /// <summary>单位位置，XZ 平面。</summary>
    public SyncVar<Vector2> Position;

    /// <summary>单位朝向方向向量，XZ 平面单位向量。</summary>
    public SyncVar<Vector2> Direction;

    /// <summary>单位碰撞半径，供技能范围判定使用。</summary>
    public SyncVar<float> BodyRadius;

    /// <summary>当前生命值。</summary>
    public SyncVar<float> Health;

    /// <summary>最大生命值。</summary>
    public SyncVar<float> MaxHealth;

    /// <summary>单位所属阵营列表，装配期一次写入的权威同步数据。</summary>
    public readonly SyncSpanSerializable<SyncCampsData> CampsData = new(() => new SyncCampsData());

    /// <summary>阵营列表只读投影，服务端与客户端同源直读；装配期一次写入后不变，每次读取新建数组。</summary>
    public IReadOnlyList<string> CampTags => CampsData.Value.ToArray();

    /// <summary>全局冷却截止的服务器逻辑 tick，客户端据此本地推算剩余时间。</summary>
    public SyncVar<ushort> GcdEndServerTick;

    /// <summary>技能个体冷却整包快照，服务端权威回写。</summary>
    public readonly SyncNetSerializable<SyncSkillCooldownSnapshot> SkillCooldowns = new(() => new SyncSkillCooldownSnapshot());

    /// <summary>当前施法技能键，空字符串表示无施法。</summary>
    public readonly SyncString SkillCasting = new();

    /// <summary>当前施法剩余读条时间，秒。</summary>
    public SyncVar<float> SkillCastRemaining;

    /// <summary>物理攻击基础系数即伤害倍率。</summary>
    public SyncVar<float> PhysicalAttackBase;

    /// <summary>魔法攻击基础系数即伤害倍率。</summary>
    public SyncVar<float> MagicAttackBase;

    /// <summary>物理伤害承受系数即减免倍率。</summary>
    public SyncVar<float> PhysicalTakePercent;

    /// <summary>魔法伤害承受系数即减免倍率。</summary>
    public SyncVar<float> MagicTakePercent;

    /// <summary>治疗强度系数即治疗倍率。</summary>
    public SyncVar<float> CureIntensity;

    /// <summary>基础移动速度。</summary>
    public SyncVar<float> BaseSpeed;

    /// <summary>单位当前持有的 Buff 列表。</summary>
    public readonly SyncList<SyncBuffData> BuffsList = [];

    /// <summary>单位拥有的技能定义列表，引用共享单位配置，装配期写入后只读，不参与网络同步。</summary>
    public IReadOnlyList<SkillDefinition> Skills {
        get; set;
    } = [];

    /// <summary>单位仇恨列表。</summary>
    public readonly SyncList<SyncHateData> HatesList = [];

    /// <summary>聚焦目标单位网络 ID，0 表示无聚焦目标。</summary>
    public SyncVar<ushort> FocusTargetNetId;

    /// <summary>玩家输入处理回调。参数：实体、输入包、帧间隔。</summary>
    public Action<UnitPawn, UnitInputPacket, float>? InputHandler {
        get;
        set;
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
