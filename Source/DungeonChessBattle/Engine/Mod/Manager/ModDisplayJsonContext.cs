using System.Text.Json.Serialization;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 展示面声明段的编译期序列化上下文，源生成器生成，零运行时反射。
/// 只覆盖展示段：数据面清单与启用集的上下文在 Battle.Mod.Manager，两者各自描述自己的段。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ModDisplayJson))]
internal partial class ModDisplayJsonContext : JsonSerializerContext;
