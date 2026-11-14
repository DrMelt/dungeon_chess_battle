using System.Numerics;
using DungeonChessBattle.Battle.Shared.Math;
using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// Entity 类型注册表。必须使用枚举类型来注册 Entity。
/// </summary>
public enum EntityClassId : ushort {
    /// <summary>战斗房间实体。</summary>
    BattleRoom = 1,

    /// <summary>单位人工输入控制器。</summary>
    UnitController = 3,

    /// <summary>单位 Pawn 实体。</summary>
    UnitPawn = 4,
}

/// <summary>
/// 构建 EntityTypesMap，服务端和客户端需使用完全相同的注册顺序。
/// </summary>
public static class EntityTypesRegistry {
    /// <summary>自定义字段类型是否已注册，幂等标志。</summary>
    private static bool _fieldTypesRegistered;

    private static EntityTypesMap<EntityClassId>? _map;

    /// <summary>
    /// 进程级 Entity 类型映射表，按需创建一次并缓存，供创建 EntityManager 使用。
    /// 实体注册顺序服务端与客户端必须完全一致；字段类型注册由静态构造保证。
    /// </summary>
    public static EntityTypesMap<EntityClassId> EntityTypesMap => _map ??= new EntityTypesMap<EntityClassId>()
            .Register(EntityClassId.BattleRoom, static p => new BattleRoomEntity(p))
            .Register(EntityClassId.UnitController, static p => new UnitController(p))
            .Register(EntityClassId.UnitPawn, static p => new UnitPawn(p));

    /// <summary>
    /// 静态构造：进程级注册 SyncVar 自定义字段类型，首次访问本类即触发。
    /// 创建 EntityManager 的必经路径 EntityTypesMap 会访问本类，因此字段注册必然先于任何 EntityManager 构造。
    /// </summary>
    static EntityTypesRegistry() {
        RegisterFieldTypes();
    }

    /// <summary>
    /// 注册 SyncVar 自定义字段类型。进程级全局，幂等，由静态构造在首次访问本类时自动调用。
    /// ⚠ LES 对未注册字段类型会静默剔除，仅内部 LogError，导致字段不参与同步，必须显式注册。
    /// </summary>
    public static void RegisterFieldTypes() {
        if (_fieldTypesRegistered)
            return;

        // System.Numerics.Vector2：LES 未内置，按官方示例需显式注册，含插值器
        EntityManager.RegisterFieldType<Vector2>(VectorMath.Lerp);
        _fieldTypesRegistered = true;
    }
}
