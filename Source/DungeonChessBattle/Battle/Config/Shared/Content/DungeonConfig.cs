using DungeonChessBattle.Battle.Shared.Camp;
using DungeonChessBattle.Battle.Config.Shared.Movement;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Config.Shared.Content;

/// <summary>
/// 敌人阵容条目：单位配置引用与出生点参数。
/// 直接引用 UnitConfig 而非字符串名，编译期类型安全，杜绝手写名称拼写错误。
/// </summary>
/// <param name="Unit">敌人单位配置，须已在内容注册表注册。</param>
/// <param name="Count">生成数量。</param>
/// <param name="SpawnBaseX">阵营出生列基准 X，同批敌人按 SpawnXSpacing 向右错开。</param>
/// <param name="SpawnXSpacing">同批敌人出生点列间距。</param>
public sealed record EnemySpawnConfig(
    UnitConfig Unit,
    int Count,
    float SpawnBaseX,
    float SpawnXSpacing);

/// <summary>
/// 玩家阵营选项：客户端在准备阶段提交选项键，服务端据此解析实际阵营列表与出生列。
/// 阵营与出生点均由副本配置权威定义，客户端不可直接设置阵营数组与出生点。
/// </summary>
/// <param name="Key">选项键，客户端协议提交值。</param>
/// <param name="Camps">该选项对应的实际阵营列表。</param>
/// <param name="SpawnBaseX">该选项的出生列基准 X，同选项玩家按 SpawnXSpacing 向右错开。</param>
/// <param name="SpawnXSpacing">该选项内相邻玩家的出生点列间距。</param>
public sealed record PlayerCampOption(
    string Key,
    IReadOnlyList<CampId> Camps,
    float SpawnBaseX,
    float SpawnXSpacing);

/// <summary>
/// 副本配置：副本键、玩家阵营选项、敌人阵营、敌人生成阵容与战场布局。
/// 纯 C# 共享配置，服务端据此生成敌人与指派玩家阵营，客户端据此决定环境表现。
/// 阵营归属由副本权威定义，单位配置不含阵营；战场布局必须显式声明，空场传 null。
/// </summary>
public sealed record DungeonConfig(
    DungeonKeyId DungeonKey,
    IReadOnlyList<PlayerCampOption> PlayerCampOptions,
    IReadOnlyList<EnemySpawnConfig> Enemies,
    CampRelationResolver RelationsResolver,
    IReadOnlyList<CampId> EnemyCamps,
    BattlefieldLayout? Layout);
