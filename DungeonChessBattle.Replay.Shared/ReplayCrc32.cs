namespace DungeonChessBattle.Replay.Shared;

/// <summary>
/// CRC-32（IEEE 802.3 反射多项式），回放容器逐块自校验用。
/// 校验对象是落盘的存储字节，故压缩与传输两段的位翻转都在解码前分家。
/// </summary>
internal static class ReplayCrc32 {
    private const uint Polynomial = 0xEDB88320;

    private static readonly uint[] _table = BuildTable();

    /// <summary>逐字节求校验和。</summary>
    public static uint Hash(ReadOnlySpan<byte> data) {
        uint crc = 0xFFFF_FFFF;
        foreach (byte b in data)
            crc = (crc >> 8) ^ _table[(crc ^ b) & 0xFF];
        return crc ^ 0xFFFF_FFFF;
    }

    private static uint[] BuildTable() {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++) {
            uint c = i;
            for (int bit = 0; bit < 8; bit++)
                c = (c & 1) != 0 ? Polynomial ^ (c >> 1) : c >> 1;
            table[i] = c;
        }

        return table;
    }
}
