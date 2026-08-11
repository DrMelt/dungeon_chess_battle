using System.Collections;
using System.Numerics;
using System.Reflection;
using DungeonChessBattle.Battle.Domain;
using DungeonChessBattle.Battle.Domain.Math;
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
    /// <summary>全部网络实体类型，与下方 Register 列表一一对应，供自检遍历。</summary>
    private static readonly Type[] EntityLogicTypes = [
        typeof(BattleRoomEntity),
        typeof(PlayerRoomEntity),
        typeof(UnitController),
        typeof(UnitPawn),
    ];

    /// <summary>自定义字段类型是否已注册，幂等标志。</summary>
    private static bool _fieldTypesRegistered;

    private static EntityTypesMap<EntityClassId>? _map;

    /// <summary>
    /// 注册 SyncVar 自定义字段类型。必须在创建任何 EntityManager 之前调用，进程级全局，幂等。
    /// ⚠ LES 对未注册字段类型会静默剔除，仅内部 LogError，导致字段不参与同步，必须显式注册。
    /// </summary>
    public static void RegisterFieldTypes() {
        if (_fieldTypesRegistered)
            return;

        // System.Numerics.Vector2：LES 未内置，按官方示例需显式注册，含插值器
        EntityManager.RegisterFieldType<Vector2>(VectorMath.Lerp);
        _fieldTypesRegistered = true;
    }

    /// <summary>
    /// 获取 LES 进程级字段类型注册表，与 LES 构建实体字段时使用的同一字典。
    /// 反射读取 internal 类型；若 LES 内部结构变化导致不可达，抛异常以 fail-fast。
    /// </summary>
    private static IDictionary GetRegisteredFieldTypeMap() {
        var field = typeof(EntityManager).Assembly
            .GetType("LiteEntitySystem.Internal.ValueTypeProcessor")
            ?.GetField("Registered", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetValue(null) as IDictionary
            ?? throw new InvalidOperationException(
                "[EntityTypesRegistry] 无法访问 LES 字段类型注册表，请检查 LiteEntitySystem 版本兼容性。");
    }

    /// <summary>
    /// 启动自检：遍历全部网络实体声明的 SyncVar&lt;T&gt; 字段，确认每个字段类型均已注册；
    /// 否则抛异常 fail-fast，把 LES 的“静默丢字段”转为启动即报错，防止新增字段类型时遗忘注册。
    /// 仅检查业务实体自身声明的字段，框架基类字段由 LES 内置类型保证。
    /// </summary>
    public static void ValidateAllFieldTypesRegistered() {
        var registered = GetRegisteredFieldTypeMap();

        foreach (var type in EntityLogicTypes) {
            foreach (var field in type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                if (!field.FieldType.IsGenericType ||
                    field.FieldType.GetGenericTypeDefinition() != typeof(SyncVar<>))
                    continue;

                var fieldType = field.FieldType.GetGenericArguments()[0];
                if (fieldType.IsEnum)
                    fieldType = Enum.GetUnderlyingType(fieldType);

                if (!registered.Contains(fieldType)) {
                    throw new InvalidOperationException(
                        $"[EntityTypesRegistry] {type.Name}.{field.Name} 使用了未注册的 SyncVar 字段类型 " +
                        $"{fieldType.Name}。请在 EntityTypesRegistry.RegisterFieldTypes() 中注册后再创建 EntityManager。");
                }
            }
        }
    }

    /// <summary>
    /// 获取 Entity 类型映射表，按需创建。
    /// 契约：客户端与服务端创建任何 EntityManager 前都必须先经本方法取得 typesMap，
    /// 因此在此统一完成字段注册与自检，可覆盖全部创建路径。
    /// </summary>
    /// <returns>已注册全部实体类型的映射表。</returns>
    public static EntityTypesMap<EntityClassId> GetOrCreateMap() {
        if (_map != null)
            return _map;

        RegisterFieldTypes();
        ValidateAllFieldTypesRegistered();

        _map = new EntityTypesMap<EntityClassId>()
            .Register(EntityClassId.BattleRoom, static p => new BattleRoomEntity(p))
            .Register(EntityClassId.PlayerRoom, static p => new PlayerRoomEntity(p))
            .Register(EntityClassId.UnitController, static p => new UnitController(p))
            .Register(EntityClassId.UnitPawn, static p => new UnitPawn(p));

        return _map;
    }
}
