namespace DungeonChessBattle.Client;

/// <summary>
/// 连接状态变化的读数：变化后的端点与连接结果。
/// </summary>
/// <param name="Host">服务器主机地址。</param>
/// <param name="Port">当前连接端口，战斗重定向后为房间端口。</param>
/// <param name="Connected">是否已连接。</param>
public sealed record ConnectionStatus(string Host, int Port, bool Connected);