namespace DungeonChessBattle.Protocol;

/// <summary>
/// 实体与网络协议字符串字段的长度限制常量。
/// 定义在共享层，作为服务端校验与客户端 UI 限制的共享业务约束。
/// </summary>
public static class EntityConstants {
    /// <summary>玩家昵称最大字符数。</summary>
    public const int MaxPlayerNameLength = 16;

    /// <summary>单位配置键最大字符数，与配置表 ConfigKey 对齐。</summary>
    public const int MaxUnitNameLength = 32;

    /// <summary>默认副本键，创建房间未指定副本时使用。</summary>
    public const string DefaultDungeonKey = "goblin_camp";
}
