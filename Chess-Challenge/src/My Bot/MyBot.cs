using ChessChallenge.API;
using System;
using System.Linq;

public class MyBot : IChessBot
{
    // Node counts for debugging purposes
    private int nodes;   // #DEBUG
    private int qNodes;  // #DEBUG

    // Save received search parameters to simplify function signatures
    private Timer timer;
    private Board board;

    private Move bestMove;
    private bool terminated;

    // Can save 4 tokens by removing this line and replacing `TABLE_SIZE` with a literal
    private const ulong TABLE_SIZE = 1 << 23;

    /// <summary>
    /// Transposition Table for caching previously computed positions during search.
    /// 
    /// To insert an entry:
    /// <code>TranspositionTable[zobrist % TABLE_SIZE] = (zobrist, depth, evaluation, nodeType, move);</code>
    /// 
    /// To retrieve an entry:
    /// <code>var (zobrist, depth, evaluation, nodeType, move) = TranspositionTable[zobrist % TABLE_SIZE];</code>
    /// 
    /// Node types:
    /// <list type="bullet">
    ///     <item>1: PV node, an exact evaluation</item>
    ///     <item>2: Beta cutoff node, a lower bound</item>
    ///     <item>3: All node, an upper bound</item>
    /// </list>
    /// </summary>
    private readonly
    (
        ulong,  // Zobrist
        int,    // Depth
        int,    // Evaluation
        int,    // Node type
        Move    // Best move
    )[] transpositionTable = new (ulong, int, int, int, Move)[TABLE_SIZE];

    private readonly int[,] historyTable = new int[7, 64];

    private static readonly decimal[] compressed =
    {
        15215810465066655233248992798m,  8089117071881235852360496940m,  9938721297025718237802733851m,  8710457497559245848775237409m,
         9320960295509831331020938775m, 18663691310616769772971236894m, 29198416909224679920076015938m, 26409434784264242148497451874m,
        25787971132820920365137285710m, 23925044871297746593197216853m, 24534352928827697620809239868m, 23939613593547706625374572114m,
        30129266437329789952855072339m, 27033212265221345376489463390m, 27034425802511090441696991831m, 25473674179343376094251015000m,
        37594511396165766148504384883m, 34809160437835226467322198653m, 33863714148503785005556591464m, 31698551765052113956184681583m,
        34794596528959947445946116708m, 70838084636249965241360084592m, 68353685150866163677120877526m, 68041815381853099293558499040m,
        67729898316616892410964334041m, 68041815419036511060572888026m,  3424971883131626255178913754m,  2793893923007420103299827715m,
         2799919625532153701646340621m,  1247639872781866742527559944m,   634757149700963786365209612m,  4973563600186848470211955462m,
        18337304487162530131240558624m, 15224206723151026829469498174m,  9632882139390068318235993383m,  9632867916732808434964045599m,
         9942357648777081944811905571m, 15543462679462424935137288224m, 22370276245645098119667467329m, 20512185762200409633611465798m,
        22677357571432763364773283138m, 18957478731381397688999363912m, 22059596569191785922885139520m, 21751325245462780089461000008m,
        23302400684076657183669438281m, 22060833904442938371414313804m, 22679775515594225955195537479m, 21751325227086403647532582730m,
        36663657478244203967693747831m, 35730347837588752113713182067m, 36352954079350911705139344757m, 35110164132793854677881090931m,
        36042255365930614164861252467m, 63690810854817569270989681011m, 65547706580265770547760057797m, 64630165217093043921825419474m,
        62763517379863439048274988741m, 60275552764954658026916924619m,  3417666362578717151626314690m,  4973620306414875382047969548m,
         5902061094922123563211297040m,  3732039359537307999320347667m,  4968741953975606623624760843m,  1869056152410222494904815120m,
    };
    private readonly byte[] pieceSquareTables = compressed.SelectMany(decimal.GetBits).Where((_, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).ToArray();

    private int EvaluateStatically()
    {
        int mgScore = 0, egScore = 0, phase = 0;

        // Loop through white and black pieces (1 for white, -1 for black)
        foreach (var sign in new[] { 1, -1 })
            for (var piece = 0; piece < 6; piece++)
            {
                ulong bitboard = board.GetPieceBitboard((PieceType)piece + 1, sign is 1);
                while (bitboard != 0)
                {
                    int index = piece * 64                                    // table start index
                        + (BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard) // square index in the table
                        ^ (sign is 1 ? 56 : 0));                              // flip board for white pieces

                    mgScore += sign * pieceSquareTables[index];
                    egScore += sign * pieceSquareTables[index + 384];
                    // Save 8 tokens by packing a lookup table into a single int
                    phase += 0b_0100_0010_0001_0001_0000 >> 4 * piece & 0xF;
                }
            }

        int eval = (mgScore * phase + egScore * (24 - phase)) / 24;
        // Tempo bonus for the current side to move
        return 10 + (board.IsWhiteToMove ? eval : -eval);
    }

    private int QuiescenceSearch(int alpha, int beta)
    {
        qNodes++; // #DEBUG

        int eval = EvaluateStatically();

        if (eval >= beta) return beta;
        if (alpha < eval) alpha = eval;

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves, !board.IsInCheck());

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

    private void SortMoves(ref Span<Move> moves, Move tableMove)
    {
        Span<int> sortKeys = stackalloc int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            sortKeys[i] = move switch
            {
                // 1. TT move
                _ when move == tableMove => 0,
                // 2. Promotions
                { IsPromotion: true } => 1,
                // 3. MVV-LVA for captures
                { IsCapture: true } => 1000 - 10 * (int)move.CapturePieceType + (int)move.MovePieceType,
                // 4. History heuristic for quiet moves
                _ => 100_000_000 - historyTable[(int)move.MovePieceType, move.TargetSquare.Index]
                // TODO: Killer moves
            };
        }
        sortKeys.Sort(moves);
    }

    private int AlphaBeta(int depth, int alpha, int beta, bool root, bool nullMoveAllowed = true)
    {
        nodes++; // #DEBUG

        // Check if time is up
        terminated = 30 * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining;

        // Early return without generating moves for draw positions
        // TODO: Should we check for insufficient material here?
        if (board.IsRepeatedPosition() || board.FiftyMoveCounter >= 100)
            return 0;

        // Also early return without generating moves if we're dropping in the QSearch
        // TODO do we want to just extend all checks instead of just depth 0 checks?
        if (depth == 0)
            if (board.IsInCheck())
                depth += 1;
            else
                return QuiescenceSearch(alpha, beta);

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves);

        // Checkmate or stalemate
        if (moves.Length == 0)
            return board.IsInCheck() ? -20_000_000 + board.PlyCount : 0;

        // Transposition table lookup
        // TODO: Should we try to probe before generating moves?
        ulong zobrist = board.ZobristKey;
        ulong TTidx = zobrist % TABLE_SIZE;

        var (TTzobrist, TTdepth, TTeval, TTtype, TTm) = transpositionTable[TTidx];

        // The TT entry is from a different position, so no best move is available
        if (TTzobrist != zobrist)
            TTm = default;
        else if (!root && TTdepth >= depth && (TTtype is 1 || TTtype is 2 && TTeval >= beta || TTtype is 3 && TTeval <= alpha))
            return TTeval;

        // Null Move Pruning: check if we beat beta even without moving
        if (nullMoveAllowed && depth > 2 && board.TrySkipTurn())
        {
            int score = -AlphaBeta(depth - 3, -beta, -beta + 1, false, false);
            board.UndoSkipTurn();
            if (score >= beta) return beta;
        }

        // Save starting alpha to detect PV and all nodes
        var oldAlpha = alpha;

        SortMoves(ref moves, TTm);

        // needed for late move reductions
        //bool isCheck = board.IsInCheck();
        int moveCount = 0;

        foreach (Move m in moves)
        {
            board.MakeMove(m);

            // internal iterative deepening
            if (depth >= 5)
                AlphaBeta(depth - 3, -beta, -alpha, false);

            // TODO is this too aggressive? it wins in self play, but other engines
            // might be able to abuse this. Maybe require at least !isCheck?
            // late move reduction
            if (depth > 2
                && moveCount++ > 4
                && !root
                //&& !isCheck
                //&& !board.IsInCheck()
                //&& !m.IsCapture
                && alpha >= -AlphaBeta(depth - 3, -beta, -alpha, false)
                )
            {
                board.UndoMove(m);
                continue;
            }

            int score = -AlphaBeta(depth - 1, -beta, -alpha, false);
            board.UndoMove(m);

            // Terminate search if time is up
            if (terminated) return 0;

            if (score >= beta)
            {
                // Save beta cutoff to the TT
                transpositionTable[TTidx] = (zobrist, depth, score, 2, m);
                // TODO: Should we update history for quiet moves only to avoid noise?
                historyTable[(int)m.MovePieceType, m.TargetSquare.Index] += depth * depth;
                return beta;
            }
            if (score > alpha)
            {
                alpha = score;
                TTm = m;
                if (root)
                    bestMove = TTm;
            }
        }

        transpositionTable[TTidx] = (zobrist, depth, alpha, oldAlpha == alpha ? 3 : 1, TTm);
        return alpha;
    }

    public Move Think(Board _board, Timer _timer)
    {
        timer = _timer;
        board = _board;

        nodes = 0;  // #DEBUG
        qNodes = 0; // #DEBUG

        historyTable.Initialize();
        bestMove = default;
        terminated = false;

        for (int depth = 1; !terminated; depth++)
        {
            var score = 5 * // #DEBUG
            AlphaBeta(depth, -100_000_000, 100_000_000, true, false);

            Console.Write($"info depth {depth} score cp {score} nodes {nodes} qnodes {qNodes}");  // #DEBUG
            Console.WriteLine($" time {timer.MillisecondsElapsedThisTurn} {bestMove}");           // #DEBUG
        }

        return bestMove == default ? board.GetLegalMoves()[0] : bestMove;
    }
}