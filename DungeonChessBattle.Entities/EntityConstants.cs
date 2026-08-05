namespace DungeonChessBattle.Entities;

/// <summary>
/// 实体与网络协议字符串字段的长度限制常量。
/// 定义在实体层，作为服务端校验与客户端 UI 限制的共享业务约束。
/// </summary>
public static class EntityConstants {
    /// <summary>玩家昵称最大字符数。</summary>
    public const int MaxPlayerNameLength = 16;

    /// <summary>单位显示名最大字符数（与配置表 displayName 对齐）。</summary>
    public const int MaxUnitNameLength = 32;

    /// <summary>房间 ID 最大字符数。</summary>
    public const int MaxRoomIdLength = 32;

    /// <summary>阵营标识最大字符数。</summary>
    public const int MaxCampLength = 16;
}
