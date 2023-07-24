using ChessChallenge.API;
using System;
using System.Linq;

public class MyBot : IChessBot
{
    // put these here to save function definition and call footprint
    Timer timer;
    Board board;

    bool done;

    ulong[] RepetitionTable = new ulong[800];

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

    void MakeMove(Move m)
    {
        // for repetition detection
        RepetitionTable[board.PlyCount] = board.ZobristKey;
        board.MakeMove(m);
    }

    void UndoMove(Move m)
    {
        board.UndoMove(m);
    }

    int Eval()
    {
        // TODO
        return 0;
    }

    void SortMoves(Span<Move> moves, Move tableMove)
    {
        // TODO
    }

    int AlphaBeta(int depth, int alpha, int beta)
    {
        ulong zobrist = board.ZobristKey;
        ulong TTidx = zobrist % TABLE_SIZE;

        // check if time is up
        // if so, we return later anyways so we dont need to return here (-3 token)
        if (30 * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining)
            done = true;

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
                return -200000 + board.PlyCount; // checkmate value
        }

        // check for draw by repetition
        for (; i < board.PlyCount;)
            if (RepetitionTable[i++] == board.ZobristKey)
                j++;
        if (j > 1)
            return 0;

        // TODO check for draw by 50 move rule

        // query transposition table
        (ulong TTzobrist, int TTdepth, int TTeval, int TTtype, Move TTm) = TranspositionTable[TTidx];

        bool isTableHit = TTzobrist == zobrist;

        j = alpha; // save starting alpha to detect PV and all nodes

        // leaf node returns static eval
        if (depth == 0)
        {
            if (isTableHit)
                return TTeval;
            i = Eval();
            TranspositionTable[TTidx] = (zobrist, 0, i, 1, TTm);
            return i;
        }

        if(isTableHit && TTdepth >= depth)
        {
            switch (TTtype) {
                case 1:
                    return TTeval;
                case 2:
                    if (TTeval >= beta) return TTeval;
                    break;
                case 3:
                    if (TTeval <= beta) return TTeval;
                    break;
            }
        }

        // TTm is now "best move"
        // might consider removing this line?? probably not though
        if (!isTableHit) TTm = Move.NullMove;

        // TODO search TT entry before generating moves? or too token expensive?

        if (!hasGeneratedMoves)
            board.GetLegalMovesNonAlloc(ref moves);
        SortMoves(moves, TTm);

        // stalemate
        // will not be found in leaf nodes, is that fine?
        if (moves.Length == 0)
            return 0;

        foreach (Move m in moves)
        {
            MakeMove(m);
            i = -AlphaBeta(depth - 1, -beta, -alpha);
            UndoMove(m);
            if (done) return 0; // time is up!!

            if (i >= beta)
            {
                TranspositionTable[TTidx] = (zobrist, depth, i, 2, m);
                return beta;
            }
            if (i > alpha)
            {
                alpha = i;
                TTm = m;
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

        done = false;
        int depth = 1;

        Move bestMove = default;
        Move[] moves = board.GetLegalMoves(); // slow move generation is fine here

        while (!done)
        {
            int bestEval = -1000000;
            SortMoves(moves, bestMove);

            foreach (Move m in moves)
            {
                MakeMove(m);
                int eval = -AlphaBeta(depth, -1000000, -bestEval);
                UndoMove(m);
                if (done) break;
                if (eval > bestEval)
                {
                    bestEval = eval;
                    bestMove = m;
                }
            }

            depth++;
        }
        return bestMove;
    }
}