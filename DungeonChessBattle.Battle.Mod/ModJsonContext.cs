using System.Text.Json.Serialization;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// mod 清单与启用集 JSON 的编译期序列化上下文，源生成器生成，零运行时反射。
/// 键命名统一 camelCase。内容不在此——内容以代码对象注册，无内容 JSON。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ModManifestJson))]
[JsonSerializable(typeof(ModEnablementJson))]
public partial class ModJsonContext : JsonSerializerContext;
