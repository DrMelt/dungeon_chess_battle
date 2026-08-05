using LiteEntitySystem;

namespace DungeonChessBattle.Entities;

/// <summary>
/// Entity 类型注册表。必须使用枚举类型来注册 Entity。
/// </summary>
public enum EntityClassId : ushort {
    /// <summary>战斗房间实体。</summary>
    BattleRoom = 1,

    /// <summary>房间内玩家实体。</summary>
    PlayerRoom = 2,

    /// <summary>单位人工输入控制器。</summary>
    UnitController = 3,

    /// <summary>单位 Pawn 实体。</summary>
    UnitPawn = 4,
}

/// <summary>
/// 构建 EntityTypesMap，服务端和客户端需使用完全相同的注册顺序。
/// </summary>
public static class EntityTypesRegistry {
    private static EntityTypesMap<EntityClassId>? _map;

    /// <summary>
    /// 获取（或按需创建）Entity 类型映射表。
    /// </summary>
    /// <returns>已注册全部实体类型的映射表。</returns>
    public static EntityTypesMap<EntityClassId> GetOrCreateMap() {
        if (_map != null)
            return _map;

        _map = new EntityTypesMap<EntityClassId>()
            .Register(EntityClassId.BattleRoom, static p => new BattleRoomEntity(p))
            .Register(EntityClassId.PlayerRoom, static p => new PlayerRoomEntity(p))
            .Register(EntityClassId.UnitController, static p => new UnitController(p))
            .Register(EntityClassId.UnitPawn, static p => new UnitPawn(p));

        return _map;
    }
}
