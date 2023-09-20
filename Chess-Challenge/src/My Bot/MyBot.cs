using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.Application.TokenCounter; // #DEBUG
using static ChessChallenge.Application.UCI;          // #DEBUG

public class MyBot : IChessBot
{
    [Tunable] public int TempoBonus { get; set; } = 46; // #DEBUG
    [Tunable] public int RFPMargin { get; set; } = 236; // #DEBUG
    [Tunable] public int LMPMargin { get; set; } = 8; // #DEBUG
    [Tunable] public int FPMargin { get; set; } = 360; // #DEBUG
    [Tunable] public int FPFixedMargin { get; set; } = 290; // #DEBUG
    [Tunable] public int SoftTimeLimit { get; set; } = 35; // #DEBUG
    [Tunable] public int HardTimeLimit { get; set; } = 3; // #DEBUG
    [Tunable] public int LMRMoves { get; set; } = 15; // #DEBUG
    [Tunable] public int LMRDepth { get; set; } = 5; // #DEBUG

    private readonly Statistics stats = new(); // #DEBUG

    private Move bestMove;

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
    private readonly int[] historyTable = new int[4096];

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

    [NoTokenCount]
    private string GetPV(Board board, Move move, int limit)
    {
        var res = " " + move.ToUCIString();
        board.MakeMove(move);
        var TTentry = transpositionTable[board.ZobristKey % TABLE_SIZE];
        if (limit > 1 && TTentry.Item1 == board.ZobristKey)
        {
            Move m = TTentry.Item5;
            if (board.GetLegalMoves().Contains(m))
            {
                res += GetPV(board, m, limit - 1);
            }
        }
        board.UndoMove(move);
        return res;
    }

    [NoTokenCount]
    private void SendReport(Board board, Timer timer, int depth, int score)
    {
        Console.Write($"info depth {depth} score cp {(5 * score) / 24} nodes {stats.Nodes}");
        Console.Write($" time {timer.MillisecondsElapsedThisTurn}");
        Console.WriteLine($" pv{GetPV(board, bestMove, 15)}");
    }

    /// <summary>
    /// The main search method of the engine. It uses <see href="https://www.chessprogramming.org/Iterative_Deepening">Iterative Deepening</see>
    /// and the "Optimal Time Management" Strategy for <see href="https://www.chessprogramming.org/Time_Management">Time Management</see>.
    /// 
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
    /// 
    /// </summary>
    /// <param name="board">The current game board.</param>
    /// <param name="timer">The timer for managing search time.</param>
    /// <returns>The best move found in the current position.</returns>
    public Move Think(Board board, Timer timer)
    {
        stats.Nodes = 0;  // #DEBUG

        for (int depth = 0; SoftTimeLimit * timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining && ++depth < 64;)
        {
            var score = // #DEBUG
            AlphaBeta(depth, -100_000_000, 100_000_000, true, 0, 0, 0, true, true);

            SendReport(board, timer, depth, score); // #DEBUG
            //stats.PrintStatistics(); // #DEBUG
        }

        return bestMove;

        int AlphaBeta(int depth, int alpha, int beta, bool needsEval, int mgScore, int egScore, int phase, bool nullMoveAllowed = true, bool root = false)
        {
            stats.Nodes++; // #DEBUG

            bool inCheck = board.IsInCheck();

            // Check extension in case of forcing sequences
            if (depth >= 0 && inCheck)
                depth += 1;

            bool inQSearch = depth <= 0;

            if (needsEval)
            {
                // Static evaluation using Piece-Square Tables (https://www.chessprogramming.org/Piece-Square_Tables)
                mgScore = egScore = phase = 0;
                // Colors are represented by the xor value of the PSQT flip
                foreach (int xor in new[] { 56, 0 })
                {
                    for (int piece = 0; piece < 6; piece++)
                    {
                        ulong bitboard = board.GetPieceBitboard((PieceType)piece + 1, xor is 56);
                        while (bitboard != 0)
                        {
                            int index = piece +                                          // piece index
                                16 * (BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard) // row of square
                                ^ xor);                                                  // flip board for white pieces

                            mgScore += pieceSquareTables[index];
                            egScore += pieceSquareTables[index + 6];
                            phase += 0b_0100_0010_0001_0001_0000 >> 4 * piece & 0xF;
                        }
                    }

                    mgScore = -mgScore;
                    egScore = -egScore;
                }
            }

            // Interpolate between game phases and add a bonus for the side to move
            int staticScore = TempoBonus + (mgScore * phase + egScore * (24 - phase)) * (board.IsWhiteToMove ? 1 : -1),
                bestScore = -20_000_000, // Mate score
                moveCount = 0,           // Number of moves played in the current position
                nodeFlag = 3,            // Upper bound flag
                score;                   // Score of the current move

            if (inQSearch)
            {
                bestScore = staticScore;
                if (staticScore >= beta) return staticScore;
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
            else if (!root && ttDepth >= depth && (ttFlag != 3 && ttScore >= beta || ttFlag != 2 && ttScore <= alpha))
                return ttScore;
            else
                staticScore = ttScore;

            bool pvNode = alpha != beta - 1;

            if (!inQSearch && !root && !pvNode && !inCheck)
            {
                // Static null move pruning (reverse futility pruning)
                if (depth < 8 && beta <= staticScore - RFPMargin * depth)
                    return staticScore;

                // Null move pruning: check if we beat beta even without moving
                if (nullMoveAllowed && depth >= 2 && staticScore >= beta)
                {
                    board.ForceSkipTurn();
                    score = -AlphaBeta(depth - 4 - depth / 6, -beta, 1 - beta, false, mgScore, egScore, phase, false);
                    board.UndoSkipTurn();
                    if (score >= beta) return beta;
                }
            }

            // Internal iterative reductions
            if (pvNode && depth >= 6 && ttMove == default)
                depth -= 2;

            var moves = board.GetLegalMoves(inQSearch);

            // Evaluate moves for Move Ordering (https://www.chessprogramming.org/Move_Ordering)
            Array.Sort(moves.Select(move =>
                // 1. PV move retrieved from the transposition table
                move == ttMove ? 0
                // 2. Queen promotion, don't bother with underpromotions
                : move.PromotionPieceType is PieceType.Queen ? 1
                // 3. Captures using MVV-LVA
                : move.IsCapture ? 1000 - 10 * (int)move.CapturePieceType + (int)move.MovePieceType
                // 4. Killer Move Heuristic
                : killerMoves[board.PlyCount] == move ? 10000
                // 5. History Heuristic with Negative Plausibility
                : 100_000_000 - historyTable[move.RawValue & 4095]
            ).ToArray(), moves);

            int latestAlpha = 0;  // #DEBUG

            foreach (Move move in moves)
            {
                if (moveCount++ > 0 && !inQSearch && !root && !pvNode && !inCheck)
                {
                    // Late move pruning: if we've tried enough moves at low depth, skip the rest
                    if (depth < 4 && moveCount >= LMPMargin * depth)
                        break;

                    // Futility pruning: if static score is far below alpha and this move is unlikely to raise it,
                    // this and later moves probably won't
                    if (depth < 6 && staticScore + FPMargin * depth + FPFixedMargin < alpha && !move.IsCapture && !move.IsPromotion)
                        break;
                }


                bool newNeedsEval = move.IsCastles || move.IsCapture || move.IsPromotion;

                var tuple = (mgScore, egScore);

                void adjustScores(PieceType piece, int squareIdx, int mult)
                {
                    int whiteMult = board.IsWhiteToMove ? mult : -mult;
                    int index = (int)piece - 1 + 16 * (squareIdx ^ (board.IsWhiteToMove ? 56 : 0));
                    tuple.mgScore += pieceSquareTables[index] * whiteMult;
                    tuple.egScore += pieceSquareTables[index + 6] * whiteMult;
                }

                adjustScores(move.MovePieceType, move.StartSquare.Index, -1);
                adjustScores(move.MovePieceType, move.TargetSquare.Index, 1);

                if (move.IsCapture)
                    adjustScores(move.CapturePieceType, move.TargetSquare.Index ^ 56, 1);

                int Search(int depth, int newAlpha) =>
                    -AlphaBeta(depth, newAlpha, -alpha, newNeedsEval, tuple.mgScore, tuple.egScore, move.IsCapture ? phase - 0b_0100_0010_0001_0001_0000 >> 4 * ((int)move.CapturePieceType - 1) & 0xF : phase);

                board.MakeMove(move);

                if (
                    // full search in qsearch
                    inQSearch
                    || moveCount == 1
                    || (
                        // late move reductions
                        moveCount <= 5
                        || depth <= 2
                        || alpha < (score = Search(depth - moveCount / LMRMoves - depth / LMRDepth - (pvNode ? 1 : 2), -alpha - 1))
                        )
                    &&
                        // zero window search
                        alpha < (score = Search(depth - 1, -alpha - 1))
                        && score < beta
                        && pvNode
                    )
                    // full window search
                    score = Search(depth - 1, -beta);

                board.UndoMove(move);

                // Avoid polling the timer at low depths, so it doesn't affect performance
                if (depth > 3 && HardTimeLimit * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining)
                    return 0;

                if (score > bestScore)
                    bestScore = score;

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

            // Checkmate or stalemate
            if (!inQSearch && moveCount < 1)
                return inCheck ? board.PlyCount - 20_000_000 : 0;

            transpositionTable[zobrist % TABLE_SIZE] = (zobrist, depth, bestScore, nodeFlag, ttMove);
            stats.TracePVOrAllNodes(nodeFlag, latestAlpha); // #DEBUG

            return bestScore;

            void UpdateHistory(Move move, int bonus)
            {
                ref int entry = ref historyTable[move.RawValue & 4095];
                entry += 32 * bonus * depth - entry * depth * depth / 512;
            }
        }
    }
}
