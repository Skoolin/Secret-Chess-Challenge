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

    void SortMoves(Span<Move> moves)
    {
        // TODO
    }

    int AlphaBeta(int depth, int alpha, int beta)
    {
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

        // leaf node returns static eval
        if (depth == 0)
        {
            int eval = Eval();
            return eval;
        }

        Move bestMove;

        // TODO search TT entry before generating moves? or too token expensive?

        if (!hasGeneratedMoves)
            board.GetLegalMovesNonAlloc(ref moves);
        SortMoves(moves);

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
                return beta;
            if (i > alpha)
                alpha = i;
        }

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
            SortMoves(moves);

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