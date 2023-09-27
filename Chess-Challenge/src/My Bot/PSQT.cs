using System;
using System.Linq;

namespace ChessChallenge.Bot;

public class PSQT
{
    /// <summary>
    /// Prints the encoded piece square table to the console.
    /// </summary>
    public static void Print()
    {
        CombineMaterialWithPSQT();
        //NormalizePSQT();

        var bytes = pieceSquareTables.Select(x => checked((byte)x)).ToArray();
        //var transposed = TransposeTable(bytes);
        var compressed = CompressAsDecimalArray(bytes);

        for (var i = 0; i < compressed.Length; i++)
        {
            Console.Write("{0,29}m, ", compressed[i]);
            if (i % 4 == 3)
                Console.WriteLine();
        }
    }

    private static byte[] TransposeTable(byte[] bytes)
    {
        byte[] result = new byte[bytes.Length];

        for (int idx = 0; idx < bytes.Length; idx++)
        {
            bool isEndgameTable = idx >= 384;
            int pieceType = (idx % 384) / 64;
            int square = idx % 64;

            int newIdx = square * 12 + pieceType + (isEndgameTable ? 6 : 0);
            result[newIdx] = bytes[idx];
        }
        return result;
    }

    /// <summary>
    /// Compresses the piece square table into a decimal array.
    /// </summary>
    private static decimal[] CompressAsDecimalArray(byte[] data)
    {
        var result = new decimal[data.Length / 12];
        for (var idx = 0; idx < result.Length; idx++)
        {
            result[idx] = new decimal(
                lo: BitConverter.ToInt32(data, idx * 12),
                mid: BitConverter.ToInt32(data, idx * 12 + 4),
                hi: BitConverter.ToInt32(data, idx * 12 + 8),
                isNegative: false,
                scale: 0);
        }
        return result;
    }

    private static void CombineMaterialWithPSQT()
    {
        for (var piece = 0; piece < 6; piece++)
        {
            for (var square = 0; square < 64; square++)
            {
                var index = piece * 64 + square;
                pieceSquareTables[index] += mgMaterial[piece];
                pieceSquareTables[index + 384] += egMaterial[piece];
            }
        }
    }

    private static void NormalizePSQT()
    {
        for (var i = 0; i < pieceSquareTables.Length; i++)
        {
            pieceSquareTables[i] /= 5;
        }
    }

    private static int[] mgMaterial = { 4, 36, 52, 59, 116, 0 };
    private static int[] egMaterial = { 16, 52, 63, 114, 226, 0 };

    private static int[] pieceSquareTables =
    {
        // Pawn MG
          0,   0,   0,   0,   0,   0,   0,   0,
         25,  27,  24,  27,  25,  21,  10,   5,
          7,  11,  16,  17,  18,  22,  17,   8,
          4,   9,   9,  11,  14,  12,  12,   7,
          2,   7,   7,  10,  10,   9,  10,   4,
          2,   7,   6,   6,   9,   7,  14,   7,
          1,   7,   5,   4,   7,  11,  15,   6,
          0,   0,   0,   0,   0,   0,   0,   0,

        // Knight MG
          0,   0,   7,  12,  17,   0,   0,   0,
         12,  15,  23,  23,  25,  34,  17,  21,
         15,  23,  26,  29,  36,  36,  28,  21,
         17,  18,  23,  26,  23,  28,  19,  23,
         14,  16,  19,  18,  20,  19,  20,  15,
         10,  14,  16,  17,  19,  17,  18,  11,
          7,   9,  12,  15,  15,  16,  13,  12,
          0,  10,   6,   9,  10,  12,  11,   0,

        // Bishop MG
          4,   0,   0,   0,   0,   0,   1,   0,
          4,  10,   8,   5,  10,  12,   9,   7,
          6,  11,  13,  16,  14,  19,  14,  13,
          5,   7,  12,  14,  13,  13,   8,   6,
          4,   7,   7,  11,  11,   8,   6,   6,
          6,   8,   8,   8,   8,   7,   8,   8,
          7,   7,   8,   5,   6,   8,  10,   7,
          2,   6,   3,   1,   2,   2,   5,   3,

        // Rook MG
         19,  18,  19,  20,  23,  25,  23,  26,
         14,  14,  18,  22,  20,  25,  22,  28,
         10,  15,  15,  17,  21,  21,  27,  23,
          6,   8,  10,  12,  13,  13,  14,  15,
          3,   3,   4,   7,   8,   5,  10,   7,
          1,   3,   4,   4,   6,   6,  11,   7,
          0,   3,   6,   6,   6,   7,  10,   2,
          5,   5,   7,   8,   9,   7,  10,   7,

        // Queen MG
          0,   2,   7,  10,  14,  17,  17,   8,
          5,   2,   3,   2,   2,  13,   8,  17,
          5,   5,   7,  10,  11,  19,  19,  15,
          3,   2,   4,   4,   5,   7,   6,   7,
          2,   3,   2,   3,   4,   3,   6,   5,
          1,   4,   3,   3,   3,   5,   7,   6,
          1,   3,   5,   5,   5,   7,   6,   7,
          1,   0,   2,   4,   3,   0,   2,   2,

        // King MG
         17,  18,  25,  12,  15,  26,  27,  37,
          7,  22,  14,  23,  23,  22,  25,   9,
          1,  23,   9,  11,  12,  25,  21,  10,
          6,   6,   7,   0,   0,   2,   3,   0,
          6,   6,   1,   0,   0,   0,   0,   0,
         12,  15,   6,   4,   4,   5,  13,   8,
         27,  20,  17,  11,  11,  14,  24,  25,
         25,  32,  27,   8,  20,  12,  29,  29,

        // Pawn EG
          0,   0,   0,   0,   0,   0,   0,   0,
         42,  37,  40,  32,  30,  31,  38,  41,
         31,  32,  27,  22,  20,  17,  26,  26,
         16,  14,  10,   8,   7,   7,  11,  11,
         11,  10,   6,   6,   5,   6,   8,   7,
          9,   9,   6,   9,   7,   7,   7,   6,
         11,  10,   8,   8,  10,   8,   7,   6,
          0,   0,   0,   0,   0,   0,   0,   0,

        // Knight EG
          0,  10,  13,  11,  12,  11,  10,   0,
          9,  13,  14,  16,  13,  10,  12,   6,
         12,  15,  19,  18,  15,  15,  13,  10,
         14,  18,  21,  21,  21,  20,  19,  12,
         13,  16,  21,  21,  21,  19,  17,  12,
          9,  14,  16,  19,  19,  15,  13,  11,
          8,  12,  13,  14,  13,  12,  10,  10,
          5,   2,  10,  11,  11,   9,   3,   8,

        // Bishop EG
          4,   6,   6,   7,   7,   5,   5,   3,
          3,   7,   7,   7,   6,   5,   7,   2,
          8,   7,   9,   7,   7,   8,   7,   6,
          7,  11,   9,  11,  10,   9,  10,   7,
          6,   9,  12,  11,  10,  10,   8,   4,
          6,   8,  10,  10,  10,   9,   6,   5,
          5,   4,   5,   7,   8,   5,   5,   1,
          2,   5,   0,   6,   5,   3,   2,   0,

        // Rook EG
         10,  11,  12,  12,  10,   8,   9,   8,
         11,  13,  13,  12,  11,   9,   9,   6,
         11,  11,  12,  11,   9,   8,   7,   6,
         12,  12,  12,  11,   9,   9,   8,   7,
         10,  11,  12,  11,   9,   9,   7,   6,
          8,   8,   8,   9,   8,   7,   3,   3,
          7,   7,   8,   8,   6,   5,   4,   5,
          6,   8,   9,   9,   7,   7,   6,   2,

        // Queen EG
          7,  13,  15,  16,  14,  10,   3,   8,
          8,  15,  21,  25,  28,  20,  18,  10,
          9,  13,  19,  19,  23,  19,  13,  14,
         11,  18,  18,  23,  25,  23,  23,  18,
         10,  15,  17,  22,  20,  20,  16,  15,
          8,   9,  15,  14,  15,  14,  10,   8,
          7,   7,   5,   8,   8,   2,   0,   0,
          6,   5,   4,   3,   4,   2,   0,   2,

        // King EG
          0,   7,  10,  15,  14,  15,  15,   0,
         13,  19,  21,  19,  21,  23,  23,  19,
         17,  21,  24,  25,  25,  24,  25,  20,
         15,  21,  24,  26,  26,  26,  24,  19,
         12,  18,  22,  25,  25,  23,  21,  16,
         10,  15,  19,  21,  21,  20,  16,  14,
          8,  12,  15,  17,  17,  16,  11,   8,
          2,   4,   8,  11,   6,  11,   5,   0,
    };
}
