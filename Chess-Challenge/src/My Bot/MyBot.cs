using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.Application.TokenCounter; // #DEBUG
using static ChessChallenge.Application.UCI;          // #DEBUG

public class MyBot : IChessBot
{
    [Tunable] public int TempoBonus { get; set; } = 46; // #DEBUG
    [Tunable] public int RFPMargin { get; set; } = 236; // #DEBUG
    [Tunable] public int FPMargin { get; set; } = 473; // #DEBUG
    [Tunable] public int FPFixedMargin { get; set; } = 173; // #DEBUG
    [Tunable] public int SoftTimeLimit { get; set; } = 35; // #DEBUG
    [Tunable] public int HardTimeLimit { get; set; } = 3; // #DEBUG

    private readonly Statistics stats = new(); // #DEBUG

    // Save received search parameters to simplify function signatures
    private Timer timer;
    private Board board;

    private Move bestMove;
    private int timerCalls;

    // Can save 4 tokens by removing this line and replacing `TABLE_SIZE` with a literal
    private const ulong TABLE_SIZE = 1 << 22;

    /// <summary>
    /// <see href="https://www.chessprogramming.org/Transposition_Table">Transposition Table</see>
    /// for caching previously computed positions during search.
    /// 
    /// <para>Node types:</para>
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
    /// <see href="https://www.chessprogramming.org/History_Heuristic">History Heuristic</see> for ordering quiet moves.
    ///
    /// Moves that often contribute to a beta cutoff have a higher score.
    /// 
    /// <para>Indexed by [<see cref="Move.MovePieceType"/>][<see cref="Move.TargetSquare"/>].</para>
    /// </summary>
    private readonly int[,,] historyTable = new int[2, 7, 64];

    /// <summary>
    /// <see href="https://www.chessprogramming.org/Killer_Move">Killer Move Heuristic</see> for ordering quiet moves.
    /// Used to retrieve moves that caused a beta cutoff in sibling nodes.
    /// 
    /// <para>Indexed by [<see cref="Board.PlyCount"/>].</para>
    /// </summary>
    private readonly Move[] killerMoves = new Move[1024]; // MAX_GAME_LENGTH = 1024

    /// <summary>
    /// Tightly packed <see href="https://www.chessprogramming.org/Piece-Square_Tables">Piece-Square Tables</see>.
    /// 
    /// Each decimal represents the piece values for one square.
    /// Because Tapered Evaluation is used, there are 2 values per piece per square. Each value is a single unsigned byte.
    /// 
    /// <para>The values are ordered as follows:</para>
    /// <list type="table">
    ///     <item>
    ///         <term>Middlegame</term>
    ///         <description>Pawn Knight Bishop Rook Queen King</description>
    ///     </item>
    ///     <item>
    ///         <term>Endgame</term>
    ///         <description>Pawn Knight Bishop Rook Queen King</description>
    ///     </item>
    /// </list>
    /// 
    /// To save tokens during unpacking, after every 12 byte square table, there are 4 empty byte values that can't be used.
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
    /// Performs static evaluation using <see href="https://www.chessprogramming.org/Piece-Square_Tables">Piece Square Tables</see>
    /// and <see href="https://www.chessprogramming.org/Tapered_Eval">Tapered Evaluation</see>.
    /// </summary>
    /// <returns>The static evaluation of the board position from the perspective of the current player.</returns>
    /// <remarks>This method will be manually inlined into the <see cref="AlphaBeta"/> method to save tokens.</remarks>
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
    /// Evaluates a move for <see href="https://www.chessprogramming.org/Move_Ordering">Move Ordering</see>.
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
    /// <param name="move">The move to evaluate.</param>
    /// <param name="tableMove">The PV move retrieved from the transposition table (or <see cref="Move.NullMove"/>).</param>
    /// <returns>The move's ranking for move ordering.</returns>
    private int GetMoveScore(Move move, Move tableMove) =>
          // 1. TT move
          move == tableMove ? 0
          // 2. Queen promotion, don't bother with underpromotions
          : move.PromotionPieceType is PieceType.Queen ? 1
          // 3. MVV-LVA for captures
          : move.IsCapture ? 1000 - 10 * (int)move.CapturePieceType + (int)move.MovePieceType
          // 4. Killer heuristic for quiet moves
          : killerMoves[board.PlyCount] == move ? 10000
          // 5. History heuristic for quiet moves
          : 100_000_000 - historyTable[board.PlyCount & 1, (int)move.MovePieceType, move.TargetSquare.Index];

    /// <summary>
    /// Performs an <see href="https://www.chessprogramming.org/Alpha-Beta">Alpha-Beta</see> search with
    /// integrated <see href="https://www.chessprogramming.org/Quiescence_Search">Quiescence Search</see> for compactness.
    /// 
    /// <para>This implementation incorporates the following search enhancements:</para>
    /// <list type="bullet">
    ///   <item><description><see href="https://www.chessprogramming.org/Check_Extensions">Check Extensions</see></description></item>
    ///   <item><description><see href="https://www.chessprogramming.org/Reverse_Futility_Pruning">Reverse Futility Pruning</see></description></item>
    ///   <item><description>Internal Iterative Reductions (TODO: details need to be added)</description></item>
    ///   <item><description><see href="https://www.chessprogramming.org/Null_Move_Pruning">Adaptive Null Move Pruning</see></description></item>
    ///   <item><description><see href="https://www.chessprogramming.org/Futility_Pruning">Futility Pruning</see></description></item>
    ///   <item><description><see href="https://www.chessprogramming.org/Late_Move_Reductions">Adaptive Late Move Reductions</see></description></item>
    ///   <item><description><see href="https://www.chessprogramming.org/Principal_Variation_Search">Principal Variation Search</see></description></item>
    /// </list>
    /// </summary>
    /// 
    /// <param name="depth">The remaining search depth.</param>
    /// <param name="alpha">The lower score bound.</param>
    /// <param name="beta">The upper score bound.</param>
    /// <param name="nullMoveAllowed">Specifies whether null move pruning is allowed.</param>
    /// <param name="root">Specifies whether the method is being called at the root of the search tree.</param>
    /// <returns>The evaluation of the position searched up to the specified <paramref name="depth"/>.</returns>
    /// 
    /// <remarks>
    /// This method will be manually inlined in the <see cref="Think(Board, Timer)"/> method to have access to the
    /// <see cref="board"/> and <see cref="timer"/> variables in the <see cref="Think(Board, Timer)"/> method's scope.
    /// </remarks>
    private int AlphaBeta(int depth, int alpha, int beta, bool nullMoveAllowed = true, bool root = false)
    {
        stats.Nodes++; // #DEBUG

        bool inCheck = board.IsInCheck();

        // Check extension in case of forcing sequences
        if (depth >= 0 && inCheck)
            depth += 1;

        bool inQSearch = depth <= 0;

        int staticScore = EvaluateStatically(),
            moveCount = 0,
            nodeFlag = 3,
            score;

        if (inQSearch)
        {
            if (staticScore >= beta) return beta;
            if (alpha < staticScore) alpha = staticScore;
        }
        else if (!root && (board.IsRepeatedPosition() || board.IsFiftyMoveDraw()))
            return 0;

        // Transposition table lookup
        ulong zobrist = board.ZobristKey;
        var (ttZobrist, ttDepth, ttScore, ttFlag, ttMove) = transpositionTable[zobrist % TABLE_SIZE];

        stats.TraceTTProbe(inQSearch, zobrist, ttZobrist); // #DEBUG

        // The TT entry is from a different position, so no best move is available
        if (ttZobrist != zobrist)
            ttMove = default;
        else if (!root && ttDepth >= depth && (ttFlag is 1 || ttFlag is 2 && ttScore >= beta || ttFlag is 3 && ttScore <= alpha))
            return ttScore;

        bool pvNode = alpha != beta - 1;

        if (!inQSearch && !root && !pvNode && !inCheck)
        {
            // Static null move pruning (reverse futility pruning)
            if (depth < 8 && beta <= staticScore - RFPMargin * depth)
                return beta;

            // Null move pruning: check if we beat beta even without moving
            if (nullMoveAllowed && depth >= 2 && staticScore >= beta)
            {
                board.ForceSkipTurn();
                score = -AlphaBeta(depth - 4 - depth / 6, -beta, 1 - beta, false);
                board.UndoSkipTurn();
                if (score >= beta) return beta;
            }
        }

        // Internal iterative reductions
        if (pvNode && depth >= 6 && ttMove == default)
            depth -= 2;

        var moves = board.GetLegalMoves(inQSearch);
        Array.Sort(moves.Select(m => GetMoveScore(m, ttMove)).ToArray(), moves);

        int latestAlpha = 0;  // #DEBUG

        foreach (Move move in moves)
        {
            moveCount++;
            // futility pruning:
            // if static eval is far below alpha and this move doesn't seem likely to raise it, 
            // this and later moves probably won't.
            if (!root
                && depth < 8
                && moveCount > 1 // don't prune TT move
                && staticScore + FPMargin * depth + FPFixedMargin < alpha // threshhold of 50 + 100 * depth centipawns
                && !move.IsCapture
                && !move.IsPromotion)
                break;

            board.MakeMove(move);

            if (
                // full search in qsearch
                inQSearch
                || moveCount == 1
                || (
                    // late move reductions
                    moveCount <= 5
                    || depth <= 2
                    || alpha < (score = -AlphaBeta(depth - (pvNode ? 2 : 1 + Math.ILogB(depth)), -alpha - 1, -alpha))
                    )
                && 
                    // zero window search
                    alpha < (score = -AlphaBeta(depth - 1, -alpha - 1, -alpha))
                    && score < beta
                    && pvNode
                )
                    // full window search
                    score = -AlphaBeta(depth - 1, -beta, -alpha);

            board.UndoMove(move);

            // Terminate search if time is up
            if ((timerCalls & 0xFF) == 0 && // only poll timer every 256 moves
                HardTimeLimit * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining) return 0;
            timerCalls++; // only do this if we didn't terminate, so TimerCalls & 0xFF is 0 in the calling function

            if (score > alpha)
            {
                latestAlpha = moveCount;  // #DEBUG

                nodeFlag = 1; // PV node
                alpha = score;
                ttMove = move;

                if (root)
                    bestMove = ttMove;

                if (score >= beta)
                {
                    stats.TraceCutoffs(moveCount);  // #DEBUG

                    nodeFlag = 2; // Fail high
                    if (!move.IsCapture)
                    {
                        UpdateHistory(move, depth);
                        killerMoves[board.PlyCount] = move;
                    }

                    break;
                }
            }

            if (!move.IsCapture)
                UpdateHistory(move, -depth);
        }

        if (!inQSearch)
        {
            // Checkmate or stalemate
            if (moveCount < 1)
                return inCheck ? -20_000_000 + board.PlyCount : 0;

            transpositionTable[zobrist % TABLE_SIZE] = (zobrist, depth, alpha, nodeFlag, ttMove);
        }

        stats.TracePVOrAllNodes(nodeFlag, latestAlpha); // #DEBUG

        return alpha;

        void UpdateHistory(Move move, int bonus)
        {
            ref int entry = ref historyTable[board.PlyCount & 1, (int)move.MovePieceType, move.TargetSquare.Index];
            entry += 32 * bonus * depth - entry * depth * depth / 512;
        }
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
    /// <para>The main method initiating the search.</para>
    /// 
    /// Uses <see href="https://www.chessprogramming.org/Iterative_Deepening">Iterative Deepening</see>
    /// and the "Optimal Time Management" Strategy for <see href="https://www.chessprogramming.org/Time_Management">Time Management</see>.
    /// 
    /// </summary>
    /// <param name="_board">The current game board.</param>
    /// <param name="_timer">The timer for managing search time.</param>
    /// <returns>The best move found in the current position.</returns>
    public Move Think(Board _board, Timer _timer)
    {
        timer = _timer;
        board = _board;

        stats.Nodes = 0;  // #DEBUG

        for (int depth = 0; SoftTimeLimit * timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining && ++depth < 64;)
        {
            var score = // #DEBUG
            AlphaBeta(depth, -100_000_000, 100_000_000, true, true);

            SendReport(depth, score); // #DEBUG
            //stats.PrintStatistics(); // #DEBUG
        }

        return bestMove;
    }
}
