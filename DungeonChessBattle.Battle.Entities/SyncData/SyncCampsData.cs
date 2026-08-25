using System.Text;
using DungeonChessBattle.Battle.Shared.Enums;
using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// 单位所属阵营列表的扁平化同步数据，ISpanSerializable 单值整包传输。
/// 阵营在实体装配期一次写入，战斗期间不变。槽位上限对齐 CampConstants 现有阵营种类。
/// </summary>
public struct SyncCampsData : ISpanSerializable {
    /// <summary>单单位最大阵营数，与领域常量单一来源对齐。</summary>
    public const int MaxCamps = CampConstants.MaxCampsPerUnit;

    /// <summary>单个阵营标识最大 UTF-8 字节数，容纳现有全部阵营字符串并留余量。</summary>
    public const int MaxCampBytes = 32;

    /// <summary>实际阵营数量。</summary>
    public byte Count;

    /// <summary>第一个阵营标识。</summary>
    public string? Camp0;

    /// <summary>第二个阵营标识。</summary>
    public string? Camp1;

    /// <summary>第三个阵营标识。</summary>
    public string? Camp2;

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 1 + MaxCamps * (2 + MaxCampBytes); // 103 bytes

    /// <summary>写入阵营列表；空、超限或非法标识即抛异常，配置故障响亮暴露。</summary>
    public void Set(IReadOnlyList<string> camps) {
        if (camps == null || camps.Count == 0 || camps.Count > MaxCamps)
            throw new InvalidOperationException(
                $"Camps count must be in 1..{MaxCamps}, got {camps?.Count ?? 0}.");
        Count = (byte)camps.Count;
        Camp0 = Camp1 = Camp2 = null;
        for (int i = 0; i < camps.Count; i++) {
            string camp = camps[i];
            if (string.IsNullOrWhiteSpace(camp) || Encoding.UTF8.GetByteCount(camp) > MaxCampBytes)
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
    public readonly string[] ToArray() {
        var result = new string[Count];
        for (int i = 0; i < Count; i++) {
            result[i] = i switch {
                0 => Camp0!,
                1 => Camp1!,
                _ => Camp2!,
            };
        }
        return result;
    }

    /// <summary>序列化到网络缓冲区。</summary>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(Count);
        writer.Put(Camp0 ?? "");
        writer.Put(Camp1 ?? "");
        writer.Put(Camp2 ?? "");
    }

    /// <summary>从网络缓冲区反序列化。</summary>
    public void Deserialize(ref SpanReader reader) {
        Count = reader.GetByte();
        Camp0 = reader.GetString();
        Camp1 = reader.GetString();
        Camp2 = reader.GetString();
    }
}
