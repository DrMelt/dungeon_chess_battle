using LiteEntitySystem;
using LiteEntitySystem.Internal;

namespace DungeonChessBattle.Entities;

/// <summary>
/// Entity 类型注册表。必须使用枚举类型来注册 Entity。
/// </summary>
public enum EntityClassId : ushort {
    BattleRoom = 1,
    PlayerRoom = 2,
    Unit = 3,
}

/// <summary>
/// 构建 EntityTypesMap，服务端和客户端需使用完全相同的注册顺序。
/// </summary>
public static class EntityTypesRegistry {
    private static EntityTypesMap<EntityClassId>? _map;

    public static EntityTypesMap<EntityClassId> GetOrCreateMap() {
        if (_map != null)
            return _map;

        _map = new EntityTypesMap<EntityClassId>()
            .Register(EntityClassId.BattleRoom, static p => new BattleRoomEntity(p))
            .Register(EntityClassId.PlayerRoom, static p => new PlayerRoomEntity(p))
            .Register(EntityClassId.Unit, static p => new UnitSyncEntity(p));

        return _map;
    }
}
