using System.Text.Json.Serialization;
using DungeonChessBattle.Battle.Mod.Content;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// mod 清单与内容 JSON 的编译期序列化上下文，源生成器生成，零运行时反射。
/// 键命名统一 camelCase，与 manifest.json / content.json 结构一一对应。
/// godot_assets.json 的序列化在 Game.Mod 项目中由 AssetsJsonContext 负责。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ModManifestJson))]
[JsonSerializable(typeof(ModContentJson))]
public partial class ModJsonContext : JsonSerializerContext;
