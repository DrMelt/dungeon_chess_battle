using System.Text;
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

    /// <summary>写入阵营列表；空、超限或非法标识即抛异常，配置故障响亮暴露。</summary>
    public void Set(IReadOnlyList<CampId> camps) {
        if (camps == null || camps.Count == 0 || camps.Count > MaxCamps)
            throw new InvalidOperationException(
                $"Camps count must be in 1..{MaxCamps}, got {camps?.Count ?? 0}.");
        Count = (byte)camps.Count;
        Camp0 = Camp1 = Camp2 = CampId.None;
        for (int i = 0; i < camps.Count; i++) {
            var camp = camps[i];
            if (string.IsNullOrWhiteSpace(camp.Value) || Encoding.UTF8.GetByteCount(camp.Value) > MaxCampBytes)
                throw new InvalidOperationException($"Invalid camp '{camp}' at index {i}.");
            switch (i) {
                case 0:
                    Camp0 = camp;
                    break;
                case 1:
                    Camp1 = camp;
                    break;
                default:
                    Camp2 = camp;
                    break;
            }
        }
    }

    /// <summary>转为数组投影，仅返回实际数量。</summary>
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

    /// <summary>从网络缓冲区反序列化。</summary>
    public void Deserialize(ref SpanReader reader) {
        Count = reader.GetByte();
        Camp0 = new CampId(reader.GetString());
        Camp1 = new CampId(reader.GetString());
        Camp2 = new CampId(reader.GetString());
    }
}
