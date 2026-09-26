using System.Buffers.Binary;

namespace SpeedcubingTrainer.Core.Solver;

/// <summary>Move and pruning tables for the two-phase solver. Roughly 5.6 MB in memory.</summary>
public sealed class SolverTables
{
    public const int Phase1MoveCount = 18; // U U2 U' R R2 R' F F2 F' D D2 D' L L2 L' B B2 B'
    public const int Phase2MoveCount = 10; // U U2 U' D D2 D' R2 F2 L2 B2

    /// <summary>Phase 2 moves expressed as phase 1 move indices (face * 3 + turn - 1).</summary>
    public static readonly int[] Phase2Moves = [0, 1, 2, 9, 10, 11, 4, 7, 13, 16];

    private const uint Magic = 0x31544353; // "SCT1"
    private const int FormatVersion = 1;

    public ushort[] TwistMove { get; } = new ushort[Coordinates.TwistCount * Phase1MoveCount];
    public ushort[] FlipMove { get; } = new ushort[Coordinates.FlipCount * Phase1MoveCount];
    public ushort[] SliceMove { get; } = new ushort[Coordinates.SliceCount * Phase1MoveCount];
    public ushort[] CornerPermMove { get; } = new ushort[Coordinates.CornerPermCount * Phase2MoveCount];
    public ushort[] UdEdgePermMove { get; } = new ushort[Coordinates.UdEdgePermCount * Phase2MoveCount];
    public ushort[] SlicePermMove { get; } = new ushort[Coordinates.SlicePermCount * Phase2MoveCount];

    public byte[] TwistSlicePrune { get; } = new byte[Coordinates.TwistCount * Coordinates.SliceCount];
    public byte[] FlipSlicePrune { get; } = new byte[Coordinates.FlipCount * Coordinates.SliceCount];
    public byte[] CornerSlicePrune { get; } = new byte[Coordinates.CornerPermCount * Coordinates.SlicePermCount];
    public byte[] EdgeSlicePrune { get; } = new byte[Coordinates.UdEdgePermCount * Coordinates.SlicePermCount];

    internal SolverTables()
    {
    }

    private IEnumerable<Array> AllTables()
    {
        yield return TwistMove;
        yield return FlipMove;
        yield return SliceMove;
        yield return CornerPermMove;
        yield return UdEdgePermMove;
        yield return SlicePermMove;
        yield return TwistSlicePrune;
        yield return FlipSlicePrune;
        yield return CornerSlicePrune;
        yield return EdgeSlicePrune;
    }

    /// <summary>Serialises to a little-endian blob: magic, version, each table's byte length and payload, then a CRC32 of everything before it.</summary>
    public byte[] Serialize()
    {
        var tables = AllTables().ToList();
        var total = 8 + tables.Sum(t => 4 + ByteLength(t)) + 4;
        var buffer = new byte[total];
        var span = buffer.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], FormatVersion);
        var offset = 8;
        foreach (var table in tables)
        {
            var length = ByteLength(table);
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], length);
            offset += 4;
            WriteTable(table, span.Slice(offset, length));
            offset += length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], Crc32.Compute(span[..offset]));
        return buffer;
    }

    /// <summary>Returns null when the blob is missing, corrupt or from another format version.</summary>
    public static SolverTables? TryDeserialize(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < 12
            || BinaryPrimitives.ReadUInt32LittleEndian(blob) != Magic
            || BinaryPrimitives.ReadInt32LittleEndian(blob[4..]) != FormatVersion
            || BinaryPrimitives.ReadUInt32LittleEndian(blob[^4..]) != Crc32.Compute(blob[..^4]))
        {
            return null;
        }
        var tables = new SolverTables();
        var offset = 8;
        foreach (var table in tables.AllTables())
        {
            if (offset + 4 > blob.Length - 4)
            {
                return null;
            }
            var length = BinaryPrimitives.ReadInt32LittleEndian(blob[offset..]);
            offset += 4;
            if (length != ByteLength(table) || offset + length > blob.Length - 4)
            {
                return null;
            }
            ReadTable(blob.Slice(offset, length), table);
            offset += length;
        }
        return offset == blob.Length - 4 ? tables : null;
    }

    private static int ByteLength(Array table) => table is ushort[] u ? u.Length * 2 : ((byte[])table).Length;

    private static void WriteTable(Array table, Span<byte> destination)
    {
        if (table is ushort[] u)
        {
            for (var i = 0; i < u.Length; i++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(destination[(i * 2)..], u[i]);
            }
        }
        else
        {
            ((byte[])table).CopyTo(destination);
        }
    }

    private static void ReadTable(ReadOnlySpan<byte> source, Array table)
    {
        if (table is ushort[] u)
        {
            for (var i = 0; i < u.Length; i++)
            {
                u[i] = BinaryPrimitives.ReadUInt16LittleEndian(source[(i * 2)..]);
            }
        }
        else
        {
            source.CopyTo((byte[])table);
        }
    }
}

internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            table[i] = c;
        }
        return table;
    }
}
