using System.Text.Json;

namespace DungeonChessBattle.Replay.Protocol;

/// <summary>
/// 回放 DTO 的 JSON 约定：服务端端点与客户端解析共用同一份选项。
/// 序列化不再由 SignalR 代劳，两端各配一份就会出"字段静默为 null"的无日志故障。
/// </summary>
public static class ReplayJson {
    /// <summary>驼峰命名 + 读取大小写不敏感。</summary>
    public static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
