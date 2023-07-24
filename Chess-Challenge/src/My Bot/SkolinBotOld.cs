using ChessChallenge.API;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

public class SkolinBotOld : IChessBot
{
    private bool done;

    const int transpositionTableSize = 32768;

    public struct ZobristEntry
    {
        public ulong zobrist;
        public int eval;
        public int depth;
        public int type;
        public Move move;
    }

    public ZobristEntry[] transpositionTable = new ZobristEntry[transpositionTableSize];

    /*
     * indices work from bottom left (0) to top right (63)
     * therefore if you "write" a square table, the values seem reversed.
     * because the large queen value in centipawns (900) doesn't fit in char, using centipawn / 5.
     * hopefully that will be enough accuracy.
     * they are 
     */

    /*
    static int[] SquareTables =
    {
    //mg_pawn_table
      0,   0,   0,   0,   0,   0,  0,   0,
     98, 134,  61,  95,  68, 126, 34, -11,
     -6,   7,  26,  31,  65,  56, 25, -20,
    -14,  13,   6,  21,  23,  12, 17, -23,
    -27,  -2,  -5,  12,  17,   6, 10, -25,
    -26,  -4,  -4, -10,   3,   3, 33, -12,
    -35,  -1, -20, -23, -15,  24, 38, -22,
      0,   0,   0,   0,   0,   0,  0,   0,

    //mg_knight_table
    -167, -89, -34, -49,  61, -97, -15, -107,
     -73, -41,  72,  36,  23,  62,   7,  -17,
     -47,  60,  37,  65,  84, 129,  73,   44,
      -9,  17,  19,  53,  37,  69,  18,   22,
     -13,   4,  16,  13,  28,  19,  21,   -8,
     -23,  -9,  12,  10,  19,  17,  25,  -16,
     -29, -53, -12,  -3,  -1,  18, -14,  -19,
    -105, -21, -58, -33, -17, -28, -19,  -23,

    //mg_bishop_table
    -29,   4, -82, -37, -25, -42,   7,  -8,
    -26,  16, -18, -13,  30,  59,  18, -47,
    -16,  37,  43,  40,  35,  50,  37,  -2,
     -4,   5,  19,  50,  37,  37,   7,  -2,
     -6,  13,  13,  26,  34,  12,  10,   4,
      0,  15,  15,  15,  14,  27,  18,  10,
      4,  15,  16,   0,   7,  21,  33,   1,
    -33,  -3, -14, -21, -13, -12, -39, -21,

    //mg_rook_table
     32,  42,  32,  51, 63,  9,  31,  43,
     27,  32,  58,  62, 80, 67,  26,  44,
     -5,  19,  26,  36, 17, 45,  61,  16,
    -24, -11,   7,  26, 24, 35,  -8, -20,
    -36, -26, -12,  -1,  9, -7,   6, -23,
    -45, -25, -16, -17,  3,  0,  -5, -33,
    -44, -16, -20,  -9, -1, 11,  -6, -71,
    -19, -13,   1,  17, 16,  7, -37, -26,

    //mg_queen_table
    -28,   0,  29,  12,  59,  44,  43,  45,
    -24, -39,  -5,   1, -16,  57,  28,  54,
    -13, -17,   7,   8,  29,  56,  47,  57,
    -27, -27, -16, -16,  -1,  17,  -2,   1,
     -9, -26,  -9, -10,  -2,  -4,   3,  -3,
    -14,   2, -11,  -2,  -5,   2,  14,   5,
    -35,  -8,  11,   2,   8,  15,  -3,   1,
     -1, -18,  -9,  10, -15, -25, -31, -50,

    //mg_king_table
    -65,  23,  16, -15, -56, -34,   2,  13,
     29,  -1, -20,  -7,  -8,  -4, -38, -29,
     -9,  24,   2, -16, -20,   6,  22, -22,
    -17, -20, -12, -27, -30, -25, -14, -36,
    -49,  -1, -27, -39, -46, -44, -33, -51,
    -14, -14, -22, -46, -44, -30, -15, -27,
      1,   7,  -8, -64, -43, -16,   9,   8,
    -15,  36,  12, -54,   8, -28,  24,  14,

    //eg_pawn_table
      0,   0,   0,   0,   0,   0,   0,   0,
    178, 173, 158, 134, 147, 132, 165, 187,
     94, 100,  85,  67,  56,  53,  82,  84,
     32,  24,  13,   5,  -2,   4,  17,  17,
     13,   9,  -3,  -7,  -7,  -8,   3,  -1,
      4,   7,  -6,   1,   0,  -5,  -1,  -8,
     13,   8,   8,  10,  13,   0,   2,  -7,
      0,   0,   0,   0,   0,   0,   0,   0,

    //eg_knight_table
    -58, -38, -13, -28, -31, -27, -63, -99,
    -25,  -8, -25,  -2,  -9, -25, -24, -52,
    -24, -20,  10,   9,  -1,  -9, -19, -41,
    -17,   3,  22,  22,  22,  11,   8, -18,
    -18,  -6,  16,  25,  16,  17,   4, -18,
    -23,  -3,  -1,  15,  10,  -3, -20, -22,
    -42, -20, -10,  -5,  -2, -20, -23, -44,
    -29, -51, -23, -15, -22, -18, -50, -64,

    //eg_bishop_table
    -14, -21, -11,  -8, -7,  -9, -17, -24,
     -8,  -4,   7, -12, -3, -13,  -4, -14,
      2,  -8,   0,  -1, -2,   6,   0,   4,
     -3,   9,  12,   9, 14,  10,   3,   2,
     -6,   3,  13,  19,  7,  10,  -3,  -9,
    -12,  -3,   8,  10, 13,   3,  -7, -15,
    -14, -18,  -7,  -1,  4,  -9, -15, -27,
    -23,  -9, -23,  -5, -9, -16,  -5, -17,

    //eg_rook_table
    13, 10, 18, 15, 12,  12,   8,   5,
    11, 13, 13, 11, -3,   3,   8,   3,
     7,  7,  7,  5,  4,  -3,  -5,  -3,
     4,  3, 13,  1,  2,   1,  -1,   2,
     3,  5,  8,  4, -5,  -6,  -8, -11,
    -4,  0, -5, -1, -7, -12,  -8, -16,
    -6, -6,  0,  2, -9,  -9, -11,  -3,
    -9,  2,  3, -1, -5, -13,   4, -20,

    //eg_queen_table
     -9,  22,  22,  27,  27,  19,  10,  20,
    -17,  20,  32,  41,  58,  25,  30,   0,
    -20,   6,   9,  49,  47,  35,  19,   9,
      3,  22,  24,  45,  57,  40,  57,  36,
    -18,  28,  19,  47,  31,  34,  39,  23,
    -16, -27,  15,   6,   9,  17,  10,   5,
    -22, -23, -30, -16, -16, -23, -36, -32,
    -33, -28, -22, -43,  -5, -32, -20, -41,

    //eg_king_table
    -74, -35, -18, -18, -11,  15,   4, -17,
    -12,  17,  14,  17,  17,  38,  23,  11,
     10,  17,  23,  15,  20,  45,  44,  13,
     -8,  22,  24,  27,  26,  33,  26,   3,
    -18,  -4,  21,  24,  27,  23,   9, -11,
    -19,  -3,  11,  21,  23,  16,   7,  -9,
    -27, -11,   4,  13,  14,   4,  -5, -17,
    -53, -34, -21, -11, -28, -14, -24, -43
};
    */

    public int[,] historyTable = new int[7, 64];

    static decimal[] Compressed =
    {
        40370370690338194552966566739m,
        22707779980965507512117006965m,
        27642477396026035805672069452m,
        23950427646339862992262813275m,
        25787990150914453223693177666m,
        9376596028510062924835017555m,
        35713412376881621293073907503m,
        29203366486490903381978616701m,
        27342649663060616728400319821m,
        22994205083796888282260790108m,
        20167703201996681006810548511m,
        18680852483081614878093753927m,
        33536005137384398058911458635m,
        26413033449174479480441693541m,
        25797694652080346406810442323m,
        22670142017313687834521197910m,
        35417160104121081949803735139m,
        28300242045798206471338488955m,
        25780674873256684065624837703m,
        20833812160788993444496691287m,
        28263841727530037519437089597m,
        32622152237437711717601728091m,
        27029519080783148773331255367m,
        25786819004235246996518760289m,
        25472465105949803386279773775m,
        25786814356307927820737860689m,
        23631317843439053129659664979m,
        21432220904891287147417715255m,
        21757384097078236560842055503m,
        18031417775343774392434575172m,
        15879648300300937827826814028m,
        27968826288998886292686588734m,
        46619398714759150228663915347m,
        38836049823229584658194077084m,
        24858345197143186402031787875m,
        24550040667682538437668523856m,
        25787990151485834288380204889m,
        10585686301571809105035875155m,
        27031927373182533053425602375m,
        23007484582130408450847035219m,
        27954380294522526804553191498m,
        18965974509359266718179480152m,
        24543958128213345109872753221m,
        23619148878235936182957199184m,
        27033202617068099506797760340m,
        24548864854419585517789403226m,
        25784320743312080881535373901m,
        23309654368625680651186360149m,
        27342697182304003985694808153m,
        25476082604913119444805833810m,
        26411805337874641635144979541m,
        23307245980312814105538809937m,
        25789203726027377389513953360m,
        28888927621059125212333034833m,
        33220480073328709051277860171m,
        31392665216862294747918394474m,
        26724846124362833600479781194m,
        20814417235621181970355608407m,
        22991663766729364545691206979m,
        27348803257939778794474003022m,
        29825854261897484530479946584m,
        24245452992579946907621679968m,
        27647294210341560826195366474m,
        19274264520435184933438313818m,
    };
    byte[] SquareTables = Compressed.SelectMany(decimal.GetBits).Where((x, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).ToArray();

    /**
    * Converts a list of bytes into a list of unsigned long integers.
    * List length must be multiple of 8.
    ulong[] CompressLong(byte[] data)
    {
        var result = new ulong[data.Length / 8];
        for (int idx = 0; idx < result.Length; idx++)
        {
            result[idx] = BitConverter.ToUInt64(data, idx*8);
        }
        return result;
    }

    /**
    * Converts a list of bytes into a list of decimal numbers.
    * List length must be multiple of 12.
    decimal[] CompressDecimal(byte[] data)
    {
        var result = new decimal[data.Length / 12];
        for (int idx = 0; idx < result.Length; idx ++)
        {
            result[idx] = new Decimal(
                BitConverter.ToInt32(data, idx*12),
                BitConverter.ToInt32(data, idx*12 + 4),
                BitConverter.ToInt32(data, idx*12 + 8),
                false,
                0);
        }
        return result;
    }

    // prints unsigned long integer list ready to be copied
    void PrintLongList(ulong[] data)
    {
        Array.ForEach(data, x => Console.WriteLine("" + x + ","));
    }

    // prints decimal list ready to be copied
    void PrintDecimalList(decimal[] data)
    {
        Array.ForEach(data, x => Console.WriteLine("" + x + "m,"));
    }
    */


    public SkolinBotOld()
    {
        int i = 0;

        /*
        byte[] bytes = new byte[SquareTables.Length];
        for (i = 0; i < SquareTables.Length; i++)
        {
            SquareTables[i] /= 2;
            SquareTables[i] += 83;
            bytes[i] = (byte)SquareTables[i];
        }
        PrintDecimalList(CompressDecimal(bytes));
        */

        for (; i < transpositionTableSize;)
        {
            transpositionTable[i++].depth = 0;
        }
    }

    private void SortMoves(Move[] moves, Move TTMove)
    {
        int[] values = new int[moves.Length];
        for (int i = 0; i < moves.Length;)
        {
            int value = 0;
            Move m = moves[i];

            if (m.Equals(TTMove))                                     // 1. TT move
                value = 1100000000;
            if (m.IsPromotion && m.PromotionPieceType == PieceType.Queen) // 2. queen promotions
                value = 1000000000;
            if (m.IsCapture && m.CapturePieceType > m.MovePieceType) // 3. "good" captures
                value = 990000000;
            if (m.IsCapture && m.CapturePieceType == m.MovePieceType) // 4. "even" captures
                value = 980000000;
            value += historyTable[(int)m.MovePieceType, m.TargetSquare.Index];
            // 6. "bad" captures
            values[i++] = value;
        }

        Array.Sort(values, moves);
        Array.Reverse(moves);
    }

    /* evaluation in centipawn
     * using piece square tables
     * 
     * TODO:
     * - open files
     * - passed pawns
     * - isolated pawns
     */
    private int Eval(Board board)
    {
        int mg_eval = 0, eg_eval = 0, i = 0, idx, pieceCount = 0;

        int[] mg_value = { 82, 337, 365, 477, 1025, 0 };
        int[] eg_value = { 94, 281, 297, 512, 936, 0 };

        var pieceLists = board.GetAllPieceLists();

        for (; i < 12; i++)
        {
            foreach (Piece p in pieceLists[i])
            {
                pieceCount++;
                int squareFile = p.Square.File, squareRank = p.Square.Rank;
                idx = (i % 6) * 32 // get correct table
                    + (i > 5 ? squareRank : squareRank ^ 56); // reverse board for white
                mg_eval += (mg_value[i % 6] + (SquareTables[idx] - 83) * 2) * (i > 5 ? -5 : 5); // scale to centipawn and reverse value of black pieces
                eg_eval += (eg_value[i % 6] + (SquareTables[idx + 384] - 83) * 2) * (i > 5 ? -5 : 5); // end game table
            }
        }

        int eval = (mg_eval * pieceCount + eg_eval * (32 - pieceCount)) / 32;

        return 25 + (board.IsWhiteToMove ? eval : -eval); // 25 bonus for side to move
    }

    void InsertZobrist(ulong idx, ulong zobrist, int depth, int eval, int type, Move move)
    {
        transpositionTable[idx].zobrist = zobrist;
        transpositionTable[idx].depth = depth;
        transpositionTable[idx].eval = eval;
        transpositionTable[idx].type = type;
        transpositionTable[idx].move = move;
    }

    int AlphaBeta(Board board, int depth, Timer timer, int alpha, int beta)
    {
        if (board.IsInCheckmate()) // if it is checkmate and our turn, we lost!
            return -200000 + board.PlyCount;

        ulong zobrist = board.ZobristKey, idx = zobrist % transpositionTableSize;
        int startingAlpha = alpha;

        if (40 * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining)
        { // limit your time
            done = true;
            return 0;
        }

        bool table_hit = false;

        ZobristEntry zobristEntry = transpositionTable[idx];
        var zobristZobrist = zobristEntry.zobrist;
        var zobristType = zobristEntry.type;
        var zobristEval = zobristEntry.eval;

        if (zobristZobrist != zobrist && board.IsDraw()) // IsDraw() calls move generation. We can avoid that if we have seen that position before.
            return 0;

        if (zobristZobrist == zobrist)
        {
            // zero depth entries dont have a move
            if (zobristEntry.depth > 0)
                table_hit = true;

            if (zobristEntry.depth >= depth)
            {
                if (zobristType == 1) // found a value we can return with
                {
                    return zobristEval >= beta ? beta : zobristEval;
                }
                if (zobristType == 3 && zobristEval >= beta) // beta cutoff
                {
                    return beta;
                }
                if (zobristType == 2 && zobristEval <= alpha) // alpha cutoff, all node
                {
                    return alpha;
                }
            }
        }

        if (depth == 0)
        {
            int eval = Eval(board);
            InsertZobrist(idx, zobrist, depth, eval, 1, Move.NullMove);
            return eval;
        }

        Move bestMove = Move.NullMove;
        Move zobristMove = Move.NullMove;
        if (table_hit)
            zobristMove = zobristEntry.move;

        Move[] moves = board.GetLegalMoves();
        SortMoves(moves, zobristMove);
        if (bestMove == Move.NullMove)
            bestMove = moves[0];

        int move_num = 0;
        foreach (Move m in moves)
        {
            move_num++;

            board.MakeMove(m);
            int eval = -AlphaBeta(board, depth - 1, timer, -beta, -alpha);
            board.UndoMove(m);
            if (done)
                return 0;

            if (eval >= beta)
            {
                InsertZobrist(idx, zobrist, depth, beta, 3, m);
                if (!zobristEntry.move.IsCapture) // update history table
                {
                    historyTable[(int)m.MovePieceType, m.TargetSquare.Index] += depth * depth;
                }
                return beta;
            }
            if (eval > alpha)
            {
                alpha = eval;
                bestMove = m;
            };
        }

        // transposition table entry:
        InsertZobrist(idx, zobrist, depth, alpha, alpha > startingAlpha ? 1 : 2, bestMove);
        return alpha;
    }

    public Move Think(Board board, Timer timer)
    {
        // reset 
        done = false;

        Move[] moves = board.GetLegalMoves();

        int max_depth = 2;
        Move finalMove = moves[0];
        for (int i = 0; i < 7; i++)
            for (int j = 0; j < 64;)
                historyTable[i, j++] >>= 3;

        while (!done)
        {
            max_depth++;
            SortMoves(moves, finalMove);
            int bestEval = -1000000;

            foreach (Move m in moves)
            {
                board.MakeMove(m);
                int eval = -AlphaBeta(board, max_depth, timer, -1000000, -bestEval);
                board.UndoMove(m);
                if (done) break;
                if (eval > bestEval)
                {
                    finalMove = m;
                    bestEval = eval;
                }
            }
        }
        return finalMove;
    }
}
