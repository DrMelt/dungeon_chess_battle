using DungeonChessBattle.Battle.Shared.ValueObjects;
using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// 单位所属阵营列表的扁平化同步数据，ISpanSerializable 单值整包传输。
/// 阵营在实体装配期一次写入，战斗期间不变。槽位上限即本结构的序列化格式约定。
/// </summary>
public struct SyncCampsData : ISpanSerializable {
    /// <summary>阵营槽位数，与序列化的 Camp0..Camp2 字段一一对应。</summary>
    public const int MaxCamps = 3;

    /// <summary>单个阵营标识最大 UTF-8 字节数，按值对象字符上限的最宽编码取足。</summary>
    public const int MaxCampBytes = CampId.MaxLength * 4;

    /// <summary>实际阵营数量。</summary>
    public byte Count {
        get; set;
    }

    /// <summary>第一个阵营标识。</summary>
    public CampId Camp0 {
        get; set;
    }

    /// <summary>第二个阵营标识。</summary>
    public CampId Camp1 {
        get; set;
    }

    /// <summary>第三个阵营标识。</summary>
    public CampId Camp2 {
        get; set;
    }

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 1 + MaxCamps * (2 + MaxCampBytes); // 391 bytes

    /// <summary>
    /// 写入阵营列表；数量越出槽位属调用方未先裁决内容，不变量断言。
    /// 阵营标识的合法性与数量由内容裁决点判定，<see cref="CampId"/> 已保证编码长度不越 <see cref="MaxCampBytes"/>。
    /// </summary>
    public void Set(IReadOnlyList<CampId> camps) {
        if (camps.Count is 0 or > MaxCamps)
            throw new InvalidOperationException($"Camps count must be in 1..{MaxCamps}, got {camps.Count}.");
        Count = (byte)camps.Count;
        Camp0 = Camp1 = Camp2 = CampId.None;
        for (int i = 0; i < camps.Count; i++) {
            switch (i) {
                case 0:
                    Camp0 = camps[i];
                    break;
                case 1:
                    Camp1 = camps[i];
                    break;
                default:
                    Camp2 = camps[i];
                    break;
            }
        }
    }

    /// <summary>转为数组投影，仅返回实际数量；槽位未填充属写入侧违约，断言暴露。</summary>
    public readonly CampId[] ToArray() {
        var result = new CampId[Count];
        for (int i = 0; i < Count; i++) {
            var camp = i switch {
                0 => Camp0,
                1 => Camp1,
                _ => Camp2,
            };
            if (camp.IsDefault)
                throw new InvalidOperationException($"阵营槽位 {i} 未填充。");
            result[i] = camp;
        }
        return result;
    }

    /// <summary>序列化到网络缓冲区。</summary>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(Count);
        writer.Put(Camp0.Value);
        writer.Put(Camp1.Value);
        writer.Put(Camp2.Value);
    }

    /// <summary>从网络缓冲区反序列化；数量越出槽位即畸形帧，在反序列化点响亮失败。</summary>
    public void Deserialize(ref SpanReader reader) {
        Count = reader.GetByte();
        if (Count is 0 or > MaxCamps)
            throw new InvalidDataException($"Camps count out of range: {Count}.");
        Camp0 = new CampId(reader.GetString());
        Camp1 = new CampId(reader.GetString());
        Camp2 = new CampId(reader.GetString());
    }
}
