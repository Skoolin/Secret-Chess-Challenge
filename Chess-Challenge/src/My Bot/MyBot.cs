using ChessChallenge.API;
using System;
using System.Linq;

public class MyBot : IChessBot
{
    // put these here to save function definition and call footprint
    Timer timer;
    Board board;

    Move BestMove;

    bool done;
    readonly ulong[] RepetitionTable = new ulong[800];

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

    static readonly decimal[] Compressed =
    {
        30445995449831333214429598547m,
        24238137566846913729271322956m,
        18339588861850990841831183170m,
        33845518446889058127066906415m,
        25471128547541693458040968525m,
        23920237298212738465575356703m,
        29818577114959539551385183563m,
        22684672555238843934297446995m,
        31374478830787750503309863011m,
        23302381592346180708512190023m,
        27661824914426437875035360061m,
        23302377036291163702074359879m,
        26103500587487099153059628623m,
        24847446121793821702496406099m,
        19892058485780872713628573519m,
        17439233312605614015817206860m,
        36052007350401232473481761619m,
        25784377578019368605253721955m,
        21447856199366454171716245337m,
        29205628018834439230888300359m,
        25162927892418910053698588746m,
        23934697351422908234375641669m,
        28580613462327693231514668884m,
        25155702855662303871390667341m,
        26410601171604923863413839961m,
        25785572263314251455196124245m,
        29824645335639590212591636560m,
        32611219443815259684250803531m,
        23293924037109377984668393802m,
        28272370378610116566492661059m,
        29513889917940488783211158360m,
        24228395084986202194831757898m,
    };
    static byte[] PieceSquareTables = Compressed.SelectMany(decimal.GetBits).Where((_, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).ToArray();

    // TODO: Apply compression if possible
    int[] MaterialMG = { 82, 337, 365, 477, 1025, 0 };
    int[] MaterialEG = { 94, 281, 297, 512, 936, 0 };

    void MakeMove(Move m)
    {
        // for repetition detection
        RepetitionTable[board.PlyCount] = board.ZobristKey;
        board.MakeMove(m);
    }

    int Eval()
    {
        int mgScore = 0, egScore = 0, pieceCount = 0;

        foreach (var list in board.GetAllPieceLists())
        {
            foreach (var piece in list)
            {
                var (file, rank) = (piece.Square.File, piece.Square.Rank);

                // Use symmetrical squares (a2 <-> h2)
                if (file > 3) file = 7 - file;

                // Flip the rank for black pieces (a2 <-> a7)
                if (!piece.IsWhite) rank = 7 - rank;

                var sign = piece.IsWhite ? 1 : -1;
                var pieceIndex = (int)piece.PieceType - 1;
                var index = pieceIndex * 16 + rank * 4 + file;

                // TODO: Instead of scaling back the piece square values, scale the material values
                mgScore += sign * (MaterialMG[pieceIndex] + (PieceSquareTables[index] - 83) * 2);
                egScore += sign * (MaterialEG[pieceIndex] + (PieceSquareTables[index + 192] - 83) * 2); // 16 (squares) * 12 (pieces) * 2 (colors)
                pieceCount++;
            }
        }

        // Apply phase interpolation
        var eval = (mgScore * pieceCount + egScore * (32 - pieceCount)) / 32;
        // Add side to move bonus
        return 25 + (board.IsWhiteToMove ? eval : -eval);
    }

    void SortMoves(Span<Move> moves, Move tableMove)
    {
        // TODO
    }

    int AlphaBeta(int depth, int alpha, int beta, bool root = false)
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

        if(!root && isTableHit && TTdepth >= depth)
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
            i = -AlphaBeta(depth - 1, -beta, root ? 1000000 : -alpha);
            board.UndoMove(m);
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

        if (root)
            BestMove = TTm;
        TranspositionTable[TTidx] = (zobrist, depth, alpha, j == alpha ? 3 : 1, TTm);
        return alpha;
    }

    public Move Think(Board _board, Timer _timer)
    {
        // set up search parameters
        timer = _timer;
        board = _board;

        done = false;
        int depth = 2;

        while (!done)
        {
            int bestEval = -1000000;
            AlphaBeta(depth, -1000000, 1000000, true);

            depth++;
        }
        return BestMove;
    }
}