using ChessChallenge.API;
using System;
using System.Linq;

public class MyBot : IChessBot
{
    // put these here to save function definition and call footprint
    Timer timer;
    Board board;

    Move BestMove;

//    int count;
//    int total_cutoffs;
//    int top_4_cutoffs;

    bool done;


    // can save 4 tokens by removing this line and replacing `TABLE_SIZE` with `32768`
    const ulong TABLE_SIZE = 32768;

    /*
     * Transposition Table
     * 
     * to insert: ulong zobrist, int depth, int eval, int type, Move m
     * TranspositionTable[zobrist % TABLE_SIZE] = (zobrist, depth, eval, type, m);
     * 
     * to retrieve:
     * (ulong TTzobrist, int TTdepth, int TTeval, int TTtype, Move TTm) = TranspositionTable[zobrist % TABLE_SIZE];
     * 
     * Types:
     * 1: PV, exact evaluation
     * 2: beta cutoff, lower bound
     * 3: all node, upper bound
     */
    readonly (
        ulong, // zobrist
        int,   // depth
        int,   // eval
        int,   // type
        Move
        )[] TranspositionTable = new (ulong, int, int, int, Move)[TABLE_SIZE];
    readonly int[,] historyTable = new int[7, 64];

    static readonly decimal[] Compressed =
    {
        13673126176016846225011251738m,
        7771107839850903524308492827m,
        44069489283999777150781956375m,
        37908813332648329697935983466m,
        33553020287227942344158441826m,
        41337478127055454253531161937m,
        40683349849577824585358870150m,
        32322343192470580098057664631m,
        53423176214669701668425402775m,
        40697885514683413831508920719m,
        50034580991902773067747787639m,
        65238188717806554420454740952m,
        61523187711879104923041190845m,
        18964759879875048954969112262m,
        11181435208056598804805530670m,
        7445939811736945933188730115m,
        18337333008578370827470903834m,
        15527670825954548325647529787m,
        14612514090171713199673913144m,
        22053551736892129898612273974m,
        17397897886453655825337762116m,
        17708592024144955820698117935m,
        21445429141499818920929541947m,
        19257263760204367883156012853m,
        35116246669590715733346776434m,
        35422062253947858140750642554m,
        50641438010293808149545578095m,
        54375923485352348354301963940m,
        52499575852024426490905012135m,
        8706845035425150603385414299m,
        8703222869073038635316421922m,
        1875100690648197429792415774m,
    };
    readonly byte[] PieceSquareTables = Compressed.SelectMany(decimal.GetBits).Where((_, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).ToArray();

    int Eval()
    {
        int mgScore = 0, egScore = 0, pieceCount = 0, i = 0;
        for (; i < 12;)
        {
            PieceType type = (PieceType)1 + (i % 6);
            bool isWhite = i++ < 6;

            ulong bitboard = board.GetPieceBitboard(type, isWhite);
            while (bitboard != 0)
            {
                int idx = BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard);
                int file = idx % 8,
                    rank = idx / 8;

                // Use symmetrical squares (a2 <-> h2)
                file ^= file > 3 ? 7 : 0;

                // Flip the rank for white pieces
                rank ^= isWhite ? 7 : 0;

                int sign = isWhite ? 1 : -1, pieceIndex = (int)type - 1;
                int index = pieceIndex * 32 + rank * 4 + file;

                mgScore += sign * PieceSquareTables[index];
                egScore += sign * PieceSquareTables[index + 192];
                pieceCount++;
            }

        }
        int eval = (mgScore * pieceCount + egScore * (32 - pieceCount)) / 32;
        // Add a tempo bonus
        return 25 + (board.IsWhiteToMove ? eval : -eval);
    }

    int QuiescenceSearch(int alpha, int beta)
    {
        int eval = Eval();

        if (eval >= beta) return beta;
        if (alpha < eval) alpha = eval;

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves, true);

        SortMoves(ref moves, default);
        foreach (var move in moves)
        {
            board.MakeMove(move);
            int score = -QuiescenceSearch(-beta, -alpha);
            board.UndoMove(move);

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        return alpha;
    }

    void SortMoves(ref Span<Move> moves, Move tableMove)
    {
        Span<int> sortKeys = stackalloc int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move m = moves[i];
            int key = m.IsCapture // 3. MVV-LVA for captures
                ? 1000 - 10 * (int)m.CapturePieceType - (int)m.MovePieceType
                // 4. quiet moves with history heuristic
                : 100000000 - historyTable[(int) m.MovePieceType, m.TargetSquare.Index];
            if (m.IsPromotion) // 2. promotions
                key = 1;
            // TODO killer moves
            if (m == tableMove) key = 0; // 1. TT move
            sortKeys[i] = key;
        }
        sortKeys.Sort(moves);
    }

    int AlphaBeta(int depth, int alpha, int beta, bool root)
    {
//        count++;
        ulong zobrist = board.ZobristKey;
        ulong TTidx = zobrist % TABLE_SIZE;

        // check if time is up
        // if so, we return later anyways so we dont need to return here (-3 token)
        done = 30 * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining;

        int i = 0, j = 0;
        bool hasGeneratedMoves = false;
        Span<Move> moves = stackalloc Move[256];

        // check for checkmate
        // if in check, we generate the moves here already
        // to see if it is checkmate
        if (board.IsInCheck())
        {
            board.GetLegalMovesNonAlloc(ref moves);
            hasGeneratedMoves = true;
            if (moves.Length == 0)
                return -20000000 + board.PlyCount; // checkmate value
        }

        // TODO: Should we check for insufficient material here?
        if (board.IsRepeatedPosition() || board.FiftyMoveCounter >= 100)
            return 0;

        // query transposition table
        var (TTzobrist, TTdepth, TTeval, TTtype, TTm) = TranspositionTable[TTidx];

        bool isTableHit = TTzobrist == zobrist;

        j = alpha; // save starting alpha to detect PV and all nodes

        // leaf node returns static eval, we don't do TT here?
        // TODO: do we extend depth on a check?
        if (depth == 0)
            return QuiescenceSearch(alpha, beta);

        if (!root && isTableHit && TTdepth >= depth)
            switch (TTtype)
            {
                case 1:
                    return TTeval;
                case 2 when TTeval >= beta:
                    return TTeval;
                case 3 when TTeval < alpha:
                    return TTeval;
            }
        else TTm = isTableHit ? TTm : BestMove;
        // TTm is now "best move"

        // TODO search TT entry before generating moves? or too token expensive?

        if (!hasGeneratedMoves)
            board.GetLegalMovesNonAlloc(ref moves);
        SortMoves(ref moves, TTm);

        // stalemate
        // will not be found in leaf nodes, is that fine?
        if (moves.Length == 0)
            return 0;

        foreach (Move m in moves)
        {
            board.MakeMove(m);
            i = -AlphaBeta(depth - 1, -beta, -alpha, false);
            board.UndoMove(m);
            if (done) return 0; // time is up!!

            if (i >= beta)
            {
//                total_cutoffs++;
//                if (moveIdx < 4)
//                    top_4_cutoffs++;
                // update TT
                TranspositionTable[TTidx] = (zobrist, depth, i, 2, m);
                // update history heuristic
                historyTable[(int)m.MovePieceType, m.TargetSquare.Index] += depth * depth;
                return beta;
            }
            if (i > alpha)
            {
                alpha = i;
                TTm = m;
                if (root)
                    BestMove = TTm;
            }
        }
        TranspositionTable[TTidx] = (zobrist, depth, alpha, j == alpha ? 3 : 1, TTm);
        return alpha;
    }

    public Move Think(Board _board, Timer _timer)
    {
        // set up search parameters
        timer = _timer;
        board = _board;

//        count = 0;

        done = false;
        int depth = 2;
        historyTable.Initialize(); // reset history table

        while (!done)
        {
//            total_cutoffs = 0;
//            top_4_cutoffs = 0;
            AlphaBeta(depth, -100000000, 100000000, true);
//            Console.WriteLine("depth: " + depth + ", nodes: " + count);
//            Console.WriteLine("" + top_4_cutoffs + "/" + total_cutoffs + " top4 : " + 100f * ((float)top_4_cutoffs / (float)total_cutoffs) + "%");
            depth++;
        }
        return BestMove;
    }
}