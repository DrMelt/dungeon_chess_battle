using System.Security.Cryptography;
using DungeonChessBattle.Server.Abstractions;

namespace DungeonChessBattle.Lobby.Server;

/// <summary>
/// 回放下载一次性凭证存储：内存字典，凭证到绑定房间与过期时间。
/// 签发后短时有效，TryConsume 取出即删，过期条目消费时惰性清理。
/// </summary>
internal sealed class ReplayDownloadTicketStore : IReplayDownloadTicketStore {
    /// <summary>凭证有效期，秒。</summary>
    private const int TicketLifetimeSeconds = 300;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (string RoomId, long ExpiryUnix)> _tickets = [];

    /// <inheritdoc />
    public string Issue(string roomId) {
        string ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        lock (_lock) {
            _tickets[ticket] = (roomId, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + TicketLifetimeSeconds);
        }
        return ticket;
    }

    /// <inheritdoc />
    public bool TryConsume(string ticket, out string roomId) {
        lock (_lock) {
            if (_tickets.TryGetValue(ticket, out var entry)) {
                _tickets.Remove(ticket);
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() <= entry.ExpiryUnix) {
                    roomId = entry.RoomId;
                    return true;
                }
            }
            roomId = null!;
            return false;
        }
    }
}
