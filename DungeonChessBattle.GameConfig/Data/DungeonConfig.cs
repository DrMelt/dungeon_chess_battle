using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Movement;

namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// 敌人阵容条目：单位配置引用与出生点参数。
/// 直接引用 UnitConfig 而非字符串名，编译期类型安全，杜绝手写名称拼写错误。
/// </summary>
/// <param name="Unit">敌人单位配置，须已在 UnitRegistry 注册。</param>
/// <param name="Count">生成数量。</param>
/// <param name="SpawnBaseX">阵营出生列基准 X，同批敌人按 SpawnXSpacing 向右错开。</param>
/// <param name="SpawnXSpacing">同批敌人出生点列间距。</param>
public sealed record EnemySpawnConfig(
    UnitConfig Unit,
    int Count,
    float SpawnBaseX = 30f,
    float SpawnXSpacing = 3f);

/// <summary>
/// 副本配置：副本键、显示名、敌人生成阵容与战场布局。
/// 纯 C# 共享配置，服务端据此生成敌人，客户端据此决定环境表现。
/// </summary>
public sealed record DungeonConfig(
    string DungeonKey,
    string DisplayName,
    string Description,
    IReadOnlyList<EnemySpawnConfig> Enemies,
    CampRelationResolver RelationsResolver,
    BattlefieldLayout? Layout = null);
