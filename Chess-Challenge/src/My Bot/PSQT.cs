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
            PieceSquareTables[i] += 79; // Max: 671
            PieceSquareTables[i] /= 3;  // Max: 223
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
        // Pawn MG
          0,   0,   0,   0,
        113, 102, 101, 101,
         -9,  32,  59,  55,
          3,  25,  32,  20,
        -21,  11,  -5,  22,
         -6,   6,   6,  -4,
         -9,  22, -14, -10,
          0,   0,   0,   0,

        // Knight MG
        190, 249, 224, 349,
        239, 315, 398, 336,
        265, 324, 337, 366,
        281, 254, 297, 287,
        217, 262, 262, 267,
        219, 245, 261, 262,
        239, 234, 240, 246,
        165, 224, 203, 202,

        // Bishop MG
        308, 247, 265, 320,
        291, 352, 356, 322,
        323, 325, 357, 349,
        272, 257, 321, 338,
        251, 287, 270, 316,
        280, 281, 283, 270,
        278, 290, 276, 270,
        232, 240, 257, 234,

        // Rook MG
        375, 333, 364, 349,
        373, 392, 431, 419,
        380, 392, 396, 438,
        351, 333, 369, 367,
        311, 328, 305, 328,
        289, 307, 305, 316,
        278, 316, 308, 307,
        329, 317, 330, 337,

        // Queen MG
        419, 409, 434, 404,
        570, 554, 538, 502,
        565, 579, 592, 568,
        545, 508, 531, 551,
        488, 554, 515, 529,
        494, 525, 530, 513,
        498, 524, 529, 517,
        517, 492, 493, 527,

        // King MG
        -79, -53, -34,  15,
          9, 138, 135, 105,
         61,  78, 130, 111,
         25,  77, 101,  77,
        -75, -28,  21,  29,
        -68, -40, -36, -48,
        -24, -20, -45, -76,
        -11,  26, -33,  -5,

        // Pawn EG
          0,   0,   0,   0,
        154, 167, 137, 132,
        156, 140, 115,  98,
         98,  86,  61,  61,
         89,  73,  66,  43,
         73,  67,  55,  71,
         91,  64,  86,  91,
          0,   0,   0,   0,

        // Knight EG
         89,  67,  87,  63,
         83,  74,  49,  76,
        103,  87, 110,  81,
        100, 128, 120, 136,
        127, 118, 122, 135,
         85,  95,  95, 121,
         59,  78,  86,  90,
         64,  40,  84,  91,

        // Bishop EG
         78, 102, 105,  85,
         92,  77,  89,  92,
         99, 112,  97,  99,
        121, 145, 117, 118,
        124, 107, 147, 128,
         82, 112, 123, 134,
         83,  97, 100, 111,
         90, 103,  92, 109,

        // Rook EG
        265, 272, 263, 266,
        272, 283, 268, 254,
        287, 288, 279, 261,
        287, 296, 291, 278,
        283, 274, 290, 290,
        263, 261, 270, 265,
        256, 253, 255, 264,
        238, 255, 264, 249,

        // Queen EG
        399, 394, 404, 410,
        415, 445, 449, 453,
        435, 457, 438, 447,
        444, 465, 456, 447,
        423, 424, 449, 445,
        434, 413, 424, 433,
        393, 404, 409, 428,
        387, 407, 403, 393,

        // King EG
         34,  47,  40,  33,
         39,  25,  23,   7,
         24,  34,  11,   9,
         28,  18,   9,   8,
         22,  28,  16,   5,
         11,  19,  20,  19,
         -8,  12,   0,  14,
        -66, -41, -32, -59,
    };
}
