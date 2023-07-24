using System;

namespace ChessChallenge.Bot;

public class PSQT
{
    /// <summary>
    /// Prints the encoded piece square table to the console.
    /// </summary>
    public static void Print()
    {
        var bytes = new byte[PieceSquareTables.Length];
        for (var i = 0; i < PieceSquareTables.Length; i++)
        {
            // Scale to fit in 8 bits
            PieceSquareTables[i] /= 2;
            PieceSquareTables[i] += 83;
            bytes[i] = (byte)PieceSquareTables[i];
        }

        foreach (var value in CompressAsDecimalArray(bytes))
            Console.WriteLine("" + value + "m,");
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

    /// <summary>
    /// Symmetrically distributed piece-square bonuses (mirrored along the Y axis).
    /// </summary>
    private static int[] PieceSquareTables =
    {
        // Pawns MG
          0,   0,   0,   0,
         98, 134,  61,  95,
         -6,   7,  26,  31,
        -14,  13,   6,  21,
        -27,  -2,  -5,  12,
        -26,  -4,  -4, -10,
        -35,  -1, -20, -23,

          0,   0,   0,   0,
        // Knights MG
        -167, -89, -34, -49,
         -73, -41,  72,  36,
         -47,  60,  37,  65,
          -9,  17,  19,  53,
         -13,   4,  16,  13,
         -23,  -9,  12,  10,
         -29, -53, -12,  -3,
        -105, -21, -58, -33,

        // Bishops MG
        -29,   4, -82, -37,
        -26,  16, -18, -13,
        -16,  37,  43,  40,
         -4,   5,  19,  50,
         -6,  13,  13,  26,
          0,  15,  15,  15,
          4,  15,  16,   0,
        -33,  -3, -14, -21,

        // Rooks MG
         32,  42,  32,  51,
         27,  32,  58,  62,
         -5,  19,  26,  36,
        -24, -11,   7,  26,
        -36, -26, -12,  -1,
        -45, -25, -16, -17,
        -44, -16, -20,  -9,
        -19, -13,   1,  17,

        // Queens MG
        -28,   0,  29,  12,
        -24, -39,  -5,   1,
        -13, -17,   7,   8,
        -27, -27, -16, -16,
         -9, -26,  -9, -10,
        -14,   2, -11,  -2,
        -35,  -8,  11,   2,
         -1, -18,  -9,  10,

        // Kings MG
        -65,  23,  16, -15,
         29,  -1, -20,  -7,
         -9,  24,   2, -16,
        -17, -20, -12, -27,
        -49,  -1, -27, -39,
        -14, -14, -22, -46,
          1,   7,  -8, -64,
        -15,  36,  12, -54,

        // Pawns EG
          0,   0,   0,   0,
        178, 173, 158, 134,
         94, 100,  85,  67,
         32,  24,  13,   5,
         13,   9,  -3,  -7,
          4,   7,  -6,   1,
         13,   8,   8,  10,
          0,   0,   0,   0,

        // Knights EG
        -58, -38, -13, -28,
        -25,  -8, -25,  -2,
        -24, -20,  10,   9,
        -17,   3,  22,  22,
        -18,  -6,  16,  25,
        -23,  -3,  -1,  15,
        -42, -20, -10,  -5,
        -29, -51, -23, -15,

        // Bishops EG
        -14, -21, -11,  -8,
         -8,  -4,   7, -12,
          2,  -8,   0,  -1,
         -3,   9,  12,   9,
         -6,   3,  13,  19,
        -12,  -3,   8,  10,
        -14, -18,  -7,  -1,
        -23,  -9, -23,  -5,

        // Rooks EG
        13, 10, 18, 15,
        11, 13, 13, 11,
         7,  7,  7,  5,
         4,  3, 13,  1,
         3,  5,  8,  4,
        -4,  0, -5, -1,
        -6, -6,  0,  2,
        -9,  2,  3, -1,

        // Queens EG
         -9,  22,  22,  27,
        -17,  20,  32,  41,
        -20,   6,   9,  49,
          3,  22,  24,  45,
        -18,  28,  19,  47,
        -16, -27,  15,   6,
        -22, -23, -30, -16,
        -33, -28, -22, -43,

        // Kings EG
        -74, -35, -18, -18,
        -12,  17,  14,  17,
         10,  17,  23,  15,
         -8,  22,  24,  27,
        -18,  -4,  21,  24,
        -19,  -3,  11,  21,
        -27, -11,   4,  13,
        -53, -34, -21, -11,
    };
}
