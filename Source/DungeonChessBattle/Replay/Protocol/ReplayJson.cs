using System.Text.Json;

namespace DungeonChessBattle.Replay.Protocol;

/// <summary>
/// 回放 DTO 的 JSON 约定：服务端端点与客户端解析共用同一份选项。
/// 序列化由两端共用本选项，不经 SignalR 默认序列化。
/// </summary>
public static class ReplayJson {
    /// <summary>驼峰命名 + 读取大小写不敏感。</summary>
    public static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
