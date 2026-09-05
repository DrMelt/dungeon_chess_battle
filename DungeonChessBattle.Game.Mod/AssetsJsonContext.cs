using System.Text.Json.Serialization;

namespace DungeonChessBattle.Game.Mod;

/// <summary>
/// godot_assets.json 的编译期序列化上下文，源生成器生成，零运行时反射。
/// 键命名统一 camelCase，与 godot_assets.json 结构一一对应。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GodotAssetsJson))]
public partial class AssetsJsonContext : JsonSerializerContext;
