namespace DungeonChessBattle.Server.Abstractions;

/// <summary>
/// 回放归档存储契约：战斗房间销毁时归档容器字节流与参与者记录主键，回放服务据此查询与下载。
/// 主键为房间 ID；本层只存字节不解释内容——展示所需元数据由归档自身的元数据块携带，
/// 摘要不再有第二份真相。参与者主键是回放归属的唯一口径，与归档字节同批写入。
/// 只暴露原语类型，不依赖回放记录模型。
/// </summary>
public interface IReplayStore {
    /// <summary>归档一场战斗的回放；重复归档同房间幂等忽略。</summary>
    void Add(string roomId, byte[] archive, IReadOnlyList<string> participantRecordIds);

    /// <summary>查询玩家记录主键参与过的房间 ID，最近归档在前。</summary>
    IReadOnlyList<string> GetRoomIdsByPlayer(string recordId);

    /// <summary>按房间 ID 取回放归档字节流，不存在时返回 false。</summary>
    bool TryGetArchive(string roomId, out byte[] archive);
}
