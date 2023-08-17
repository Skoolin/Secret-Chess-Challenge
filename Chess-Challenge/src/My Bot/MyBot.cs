using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.Application.TokenCounter; // #DEBUG
using static ChessChallenge.Application.UCI;          // #DEBUG

public class MyBot : IChessBot
{
    [Tunable] public int TempoBonus { get; set; } = 46; // #DEBUG
    [Tunable] public int RFPMargin { get; set; } = 337; // #DEBUG
    [Tunable] public int FPMargin { get; set; } = 473; // #DEBUG
    [Tunable] public int FPFixedMargin { get; set; } = 173; // #DEBUG
    [Tunable] public int SoftTimeLimit { get; set; } = 35; // #DEBUG
    [Tunable] public int HardTimeLimit { get; set; } = 3; // #DEBUG

    private readonly Statistics stats = new(); // #DEBUG

    // Save received search parameters to simplify function signatures
    private Timer timer;
    private Board board;

    private Move bestMove;
    private int TimerCalls;

    // Can save 4 tokens by removing this line and replacing `TABLE_SIZE` with a literal
    private const ulong TABLE_SIZE = 1 << 22;

    /// <summary>
    /// Transposition Table for caching previously computed positions during search.
    /// https://www.chessprogramming.org/Transposition_Table
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

    /// <summary>
    /// History Heuristic for ordering quiet moves.
    /// https://www.chessprogramming.org/History_Heuristic
    /// 
    /// Moves that often contribute to a beta cutoff have a higher score.
    /// 
    /// Indexed by:
    /// <code>historyTable[pieceType, targetSquare]</code>
    /// </summary>
    private readonly int[,] historyTable = new int[7, 64];

    /// <summary>
    /// Negative Plausibility: Extension of History Heuristic.
    /// http://www.aifactory.co.uk/newsletter/2007_01_neg_plausibility.htm
    /// 
    /// Stores quiets that didn't raise alpha and that "delayed" a beta cutoff from occurring.
    /// These moves will get a penalty in the historyTable when a move with lower score causes a beta cutoff to occur.
    /// </summary>
    private readonly Move[] badQuiets = new Move[512];

    /// <summary>
    /// Killer Move Heuristic for ordering quiet moves.
    /// https://www.chessprogramming.org/Killer_Move
    /// 
    /// Used to retrieve moves that caused a beta cutoff in sibling nodes.
    /// 
    /// Indexed by [board.PlyCount]
    /// </summary>
    private readonly (Move, Move)[] killerMoves = new (Move, Move)[1024]; // MAX_GAME_LENGTH = 1024


    /// <summary>
    /// Tightly packed Piece Square Tables:
    /// https://www.chessprogramming.org/Piece-Square_Tables
    /// 
    /// Each decimal represents the piece values for one square.
    /// Because we use Tapered Eval, we need 2 values per piece per square.
    /// Each value is a single unsigned byte.
    /// 
    /// The values are ordered as follows:
    /// <list type="table">
    ///     <item>
    ///         <term>middlegame</term>
    ///         <description>pawn knight bishop rook queen king</description>
    ///     </item>
    ///     <item>
    ///         <term>endgame</term>
    ///         <description>pawn knight bishop rook queen king</description>
    ///     </item>
    /// </list>
    /// 
    /// To save tokens during unpacking, after every 12 byte square table there are 4 empty byte values that can't be used.
    /// There are 86 tokens in total: 64 decimal literals and 22 tokens to declare and unpack the table.
    /// </summary>
    private readonly byte[] pieceSquareTables = new[] {
          282266529097455053078799364m,  2455919912745106374728623108m,  3386797516431034209084386052m,  4935431509659988786767212548m,
         4623519203538509630205605124m,  4928159028382896809030460420m,  4919701269940020779892941828m,   283465991737381893962998788m,
         4306785287174623693249196061m,  6172167345646778025007526687m,  6798390920280049164742310684m,  6184251881691207953985125151m,
         6806843937762779303503609117m,  7416133087452950235235501593m,  7413715272853297914227537166m,  6166089567246312935719123209m,
         5545934344726408926104007435m,  6788710069059982090422926095m,  7724423412988483705405259284m,  8033903663476395000086151445m,
         8038729921805153149300918294m,  7724404504784780671022745626m,  8026631218733624710112886805m,  6480410354417102158320187660m,
         4929386880782585395265746184m,  6794759494522601928612591117m,  7723214487308196977710217997m,  8348224450575394892306071055m,
         8350632839034632866990471954m,  8348214968876604148506771472m,  7729240245243244616223569680m,  6175765788827324583463500555m,
         3999713462245760773649871366m,  5862672928194510734078194699m,  7103035597184991926547068683m,  8037530514933872240074110478m,
         8035103200114651698059753486m,  7416133180328127843165550349m,  6792317975636973077153855502m,  5243679204196604490360173320m,
         3378316145942088905196711430m,  4926950158122208307057537547m,  6172143789362025367686886410m,  6789909605768598753627616522m,
         6791113809221167563237242637m,  6480415132478751653932774667m,  5237620444964642779316368914m,  4616232555091577144835059467m,
         2758132459595079043790875397m,  3996072480721656458214321419m,  4922114399431180703583973385m,  5544711233498253872944722696m,
         5544701807140537563279405835m,  5227958464622172391882896399m,  3678110841365349248083570963m,  2749660460480540245411704842m,
          900008696921407705696314372m,  1517779290601516084563160580m,  2754515034776499494753610244m,  3681761249173421225934138628m,
         2135535662706613888346369540m,  3680542823136497829114425348m,  1821210171323644351172128516m,   276184047857016610036851716m,
    }.SelectMany(decimal.GetBits).SelectMany(BitConverter.GetBytes).ToArray();

    /// <summary>
    /// Static evaluation using Piece Square Tables and Tapered Evaluation
    /// https://www.chessprogramming.org/Tapered_Eval
    /// 
    /// This will be manually inlined into the AlphaBeta() function to save tokens
    /// </summary>
    /// <returns>static evaluation of board position from the perspective of current player</returns>
    private int EvaluateStatically()
    {
        int mgScore = 0, egScore = 0, phase = 0;

        // Colors are represented by the xor value of the PSQT flip
        foreach (var xor in new[] { 56, 0 })
        {
            for (var piece = 0; piece < 6; piece++)
            {
                ulong bitboard = board.GetPieceBitboard((PieceType)piece + 1, xor is 56);
                while (bitboard != 0)
                {
                    int index = piece +                                          // piece index
                        16 * (BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard) // row of square
                        ^ xor);                                                  // flip board for white pieces

                    mgScore += pieceSquareTables[index];
                    egScore += pieceSquareTables[index + 6];
                    // Save 8 tokens by packing a lookup table into a single int
                    phase += 0b_0100_0010_0001_0001_0000 >> 4 * piece & 0xF;
                }
            }

            mgScore = -mgScore;
            egScore = -egScore;
        }

        // Interpolate between game phases and add a bonus for the side to move
        return TempoBonus + (mgScore * phase + egScore * (24 - phase)) * (board.IsWhiteToMove ? 1 : -1);
    }

    /// <summary>
    /// Evaluates move for move ordering.
    /// https://www.chessprogramming.org/Move_Ordering
    /// 
    /// Move ordering features are:
    /// <list type="number">
    ///     <item>Transposition Table</item>
    ///     <item>Promotions</item>
    ///     <item>Captures using MVV-LVA</item>
    ///     <item>Killer Move Heuristic</item>
    ///     <item>History Heuristic with Negative Plausibility</item>
    /// </list>
    /// </summary>
    /// <param name="move">the move to evaluate</param>
    /// <param name="tableMove">the PV move retrieved from the transposition table (or Move.NullMove)</param>
    /// <returns>move ranking for move ordering</returns>
    private int GetMoveScore(Move move, Move tableMove) =>
          // 1. TT move
          move == tableMove ? 0
          // 2. Queen promotion, don't bother with underpromotions
          : move.PromotionPieceType is PieceType.Queen ? 1
          // 3. MVV-LVA for captures
          : move.IsCapture ? 1000 - 10 * (int)move.CapturePieceType + (int)move.MovePieceType
          // 4. Killer heuristic for quiet moves
          : killerMoves[board.PlyCount].Item1 == move || killerMoves[board.PlyCount].Item2 == move ? 10000
          // 5. History heuristic for quiet moves
          : 100_000_000 - historyTable[(int)move.MovePieceType, move.TargetSquare.Index];

    /// <summary>
    /// alpha-beta-search implementation combined with quiescence-search for compactness.
    /// https://www.chessprogramming.org/Alpha-Beta
    /// https://www.chessprogramming.org/Quiescence_Search
    /// 
    /// We implemented the following search enhancements:
    /// <list type="bullet">
    ///     <item><term>Check Extensions</term><description>https://www.chessprogramming.org/Check_Extensions</description></item>
    ///     <item><term>Reverse Futility Pruning</term><description>https://www.chessprogramming.org/Reverse_Futility_Pruning</description></item>
    ///     <item><term>Internal Iterative Deepening</term><description>https://www.chessprogramming.org/Internal_Iterative_Deepening</description></item>
    ///     <item><term>Internal Iterative Reductions</term><description></description></item>
    ///     <item><term>Adaptive Null Move Pruning</term><description>https://www.chessprogramming.org/Null_Move_Pruning</description></item>
    ///     <item><term>Futility Pruning</term><description>https://www.chessprogramming.org/Futility_Pruning</description></item>
    ///     <item><term>Adaptive Late Move Reductions</term><description>https://www.chessprogramming.org/Late_Move_Reductions</description></item>
    ///     <item><term>Principal Variation Search</term><description>https://www.chessprogramming.org/Principal_Variation_Search</description></item>
    /// </list>
    /// 
    /// This method is declared in the 'Think()' method to have access to the 'board' and 'timer' variables in the 'Think()' method's scope.
    /// 
    /// </summary>
    /// <param name="depth">remaining search depth</param>
    /// <param name="alpha">lower score bound</param>
    /// <param name="beta">upper score bound</param>
    /// <param name="nullMoveAllowed"></param>
    /// <param name="root"></param>
    /// <returns>evaluation of the position searched up to <paramref name="depth"/></returns>
    private int AlphaBeta(int depth, int alpha, int beta, bool nullMoveAllowed = true, bool root = false)
    {
        stats.Nodes++; // #DEBUG

        // Check extension in case of forcing sequences
        if (depth >= 0 && board.IsInCheck())
            depth += 1;

        bool inQSearch = depth <= 0;
        int eval = EvaluateStatically();

        if (inQSearch)
        {
            if (eval >= beta) return beta;
            if (alpha < eval) alpha = eval;
        }
        else
        {
            // reverse futility pruning
            if (!board.IsInCheck() && depth < 8 && beta <= eval - RFPMargin * depth)
                return eval;
            // Early return without generating moves for draw positions
            if (!root && (board.IsRepeatedPosition() || board.IsFiftyMoveDraw()))
                return 0;
        }

        // Transposition table lookup
        ulong zobrist = board.ZobristKey,
            TTidx = zobrist % TABLE_SIZE;

        // internal iterative deepening
        if (depth >= 4 && transpositionTable[TTidx].Item1 != zobrist)
            // internal iterative reductions (--depth)
            AlphaBeta(--depth - 2, alpha, beta, nullMoveAllowed, root);

        var (TTzobrist, TTdepth, TTeval, TTtype, TTm) = transpositionTable[TTidx];

        stats.TraceTTProbe(inQSearch, zobrist, TTzobrist); // #DEBUG

        // The TT entry is from a different position, so no best move is available
        if (TTzobrist != zobrist)
            TTm = default;
        else if (!root && TTdepth >= depth && (TTtype is 1 || TTtype is 2 && TTeval >= beta || TTtype is 3 && TTeval <= alpha))
            return TTeval;

        int TTnodeType = 3,
            moveCount = 0,
            score,
            badQuietCount = 0;

        // Null Move Pruning: check if we beat beta even without moving
        if (nullMoveAllowed && depth > 2 && eval >= beta && board.TrySkipTurn())
        {
            score = -AlphaBeta(depth - 3 - depth / 6, -beta, 1 - beta, false);
            board.UndoSkipTurn();
            if (score >= beta) return beta;
        }

        var pvNode = alpha != beta - 1;
        var moves = board.GetLegalMoves(inQSearch);
        Array.Sort(moves.Select(m => GetMoveScore(m, TTm)).ToArray(), moves);

        int latestAlpha = 0;  // #DEBUG

        foreach (Move m in moves)
        {
            moveCount++;
            // futility pruning:
            // if static eval is far below alpha and this move doesn't seem likely to raise it, 
            // this and later moves probably won't.
            if (!root
                && depth < 8
                && moveCount > 1 // don't prune TT move
                && eval + FPMargin * depth + FPFixedMargin < alpha // threshhold of 50 + 100 * depth centipawns
                && !m.IsCapture
                && !m.IsPromotion)
                break;

            board.MakeMove(m);

            // late move reduction
            if (depth <= 2
                || moveCount <= 5
                || alpha < (score = -AlphaBeta(depth - (pvNode ? 2 : 1 + Math.ILogB(depth)), -alpha - 1, -alpha)))

                // zero window search
                if (root
                || moveCount > 1
                || alpha < (score = -AlphaBeta(depth - 1, -alpha - 1, -alpha))
                && score < beta)

                    // full window search
                    score = -AlphaBeta(depth - 1, -beta, -alpha);

            board.UndoMove(m);

            // Terminate search if time is up
            if ((TimerCalls & 0xFF) == 0 && // only poll timer every 256 moves
                HardTimeLimit * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining) return 0;
            TimerCalls++; // only do this if we didn't terminate, so TimerCalls & 0xFF is 0 in the calling function

            if (score > alpha)
            {
                latestAlpha = moveCount;  // #DEBUG

                TTnodeType = 1; // PV node
                alpha = score;
                TTm = m;

                if (root)
                    bestMove = TTm;

                if (score >= beta)
                {
                    stats.TraceCutoffs(moveCount);  // #DEBUG

                    TTnodeType = 2; // Fail high
                    if (!m.IsCapture)
                    {
                        while (badQuietCount-- > 0)
                            historyTable[(int)badQuiets[badQuietCount].MovePieceType, badQuiets[badQuietCount].TargetSquare.Index] -= depth * depth;
                        historyTable[(int)m.MovePieceType, m.TargetSquare.Index] += depth * depth;
                        killerMoves[board.PlyCount] = (m, killerMoves[board.PlyCount].Item1);
                    }
                    break;
                }
            }
            if (!m.IsCapture)
                badQuiets[badQuietCount++] = m;
        }

        if (!inQSearch)
        {
            // Checkmate or stalemate
            if (moveCount < 1)
                return board.IsInCheck() ? -20_000_000 + board.PlyCount : 0;

            transpositionTable[TTidx] = (zobrist, depth, alpha, TTnodeType, TTm);
        }

        stats.TracePVOrAllNodes(TTnodeType, latestAlpha); // #DEBUG

        return alpha;
    }

    [NoTokenCount]
    private string GetPV(Move move, int limit)
    {
        var res = " " + move.ToUCIString();
        board.MakeMove(move);
        var TTentry = transpositionTable[board.ZobristKey % TABLE_SIZE];
        if (limit > 1 && TTentry.Item1 == board.ZobristKey)
        {
            Move m = TTentry.Item5;
            if (board.GetLegalMoves().Contains(m))
            {
                res += GetPV(m, limit - 1);
            }
        }
        board.UndoMove(move);
        return res;
    }

    [NoTokenCount]
    private void SendReport(int depth, int score)
    {
        Console.Write($"info depth {depth} score cp {(5 * score) / 24} nodes {stats.Nodes}");
        Console.Write($" time {timer.MillisecondsElapsedThisTurn}");
        Console.WriteLine($" pv{GetPV(bestMove, 15)}");
    }

    [NoTokenCount]
    public Move Think(Board _board, int maxDepth)
    {
        timer = new Timer(100000000);
        board = _board;

        var score = AlphaBeta(maxDepth, -100_000_000, 100_000_000, true, true);
        SendReport(maxDepth, score);
        return bestMove;
    }

    /// <summary>
    /// The main method initiating the search.
    /// Uses Aspiration Window Search to bound the Alpha-Beta Search.
    /// https://www.chessprogramming.org/Aspiration_Windows
    /// 
    /// Uses Iterative Deepening and the "Optimal Time Management" Strategy for Time Management
    /// https://www.chessprogramming.org/Iterative_Deepening
    /// 
    /// </summary>
    /// <param name="_board"></param>
    /// <param name="_timer"></param>
    /// <returns>the best move found in the current position</returns>
    public Move Think(Board _board, Timer _timer)
    {
        timer = _timer;
        board = _board;

        stats.Nodes = 0;  // #DEBUG

        for (int depth = 0; timer.MillisecondsElapsedThisTurn * SoftTimeLimit < timer.MillisecondsRemaining && ++depth < 64;)
        {
            var score = // #DEBUG
            AlphaBeta(depth, -100_000_000, 100_000_000, true, true);

            SendReport(depth, score); // #DEBUG
            //stats.PrintStatistics(); // #DEBUG
        }

        return bestMove;
    }
}
