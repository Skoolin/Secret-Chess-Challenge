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
         9628032156016814535290913796m,  3739330748943827784153569565m,  4346140379023522695215910152m,  3426147959350246416118058254m,
         1242794706542687070007659269m, 11185152669416276013139362820m, 20191758603732251806925534000m, 18326409823835205121727285320m,
        16465806633716380841595581490m, 14914764344115234867403699511m, 16156331252012037324214513188m, 18333663433733127431584560180m,
        20503661576453411624395554618m, 18020532775865811861281456449m, 17713460821314345596499016762m, 17090840319640865196304383034m,
        25161719169152913031866240334m, 25482117696392597169675261007m, 20502466910617455769214600001m, 20510943612998979672577753155m,
        20815588178586217362537659963m, 38537558833788493959778353732m, 39144382761321243443688076921m, 38214728249832672635115767679m,
        36973147192274645216514176886m, 38214728249905012794900314487m,  3744128588073985660939564149m,  2815692551881497707894479375m,
            8490926343178518757185281m,               288097816478208m,  3424981792940117276810940172m,  9010181171504603017860615691m,
        14923231673815173664332582928m, 13049301801847373640435642158m,  6835389865644938206073462304m,  6836584550432607576111191573m,
         4971178824858753753719249435m, 16168472605764084778784526352m, 21750102004030189768527724861m, 19893215724038636353437647683m,
        22055955459119738728114373697m, 19263327445006937425675633479m, 21747693652605739275697141305m, 20201472844570441812592051270m,
        22989284137175361503766267463m, 20821675475941777185291454537m, 21746475301080316520665007941m, 19576448732575938594042299463m,
        39149246871792652793025559932m, 37285059609438012619756370813m, 38838543472899645308311338622m, 36351764135737047554125822843m,
        38215932434766440880599103865m, 72697455234101068101695601017m, 76121147229683932814602924522m, 75816545442890526588652221945m,
        74568867569745798387815674348m, 70217910487879854167801262321m,  4654397478574698875870177256m,  5908129483642450534317625102m,
         8075723922871239506480600337m,  4977256676758788300349119002m,  5279435871242164628299648778m,     6096686604738895230472209m,
    }.SelectMany(d => decimal.GetBits(d).Take(3)).SelectMany(BitConverter.GetBytes).ToArray();

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
            AlphaBeta(depth, -100_000_000, 100_000_000, true, true);

            SendReport(board, timer, depth, score); // #DEBUG
            //stats.PrintStatistics(); // #DEBUG
        }

        return bestMove;

        int AlphaBeta(int depth, int alpha, int beta, bool nullMoveAllowed = true, bool root = false)
        {
            stats.Nodes++; // #DEBUG

            bool inCheck = board.IsInCheck();

            // Check extension in case of forcing sequences
            if (depth >= 0 && inCheck)
                depth += 1;

            bool inQSearch = depth <= 0;

            // Static evaluation using Piece-Square Tables (https://www.chessprogramming.org/Piece-Square_Tables)
            int mgScore = 0, egScore = 0, phase = 0;
            // Colors are represented by the xor value of the PSQT flip
            foreach (int xor in new[] { 56, 0 })
            {
                for (int piece = 0; piece < 6; piece++)
                {
                    ulong bitboard = board.GetPieceBitboard((PieceType)piece + 1, xor is 56);
                    while (bitboard != 0)
                    {
                        int index = piece * 64                                   // table start index
                            + BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard) // square index in the table
                            ^ xor;                                               // flip board for white pieces

                        mgScore += pieceSquareTables[index];
                        egScore += pieceSquareTables[index + 384];
                        phase += 0b_0100_0010_0001_0001_0000 >> 4 * piece & 0xF;
                    }
                }

                mgScore = -mgScore;
                egScore = -egScore;
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
                    score = -AlphaBeta(depth - 4 - depth / 6, -beta, 1 - beta, false);
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

                board.MakeMove(move);

                if (
                    // full search in qsearch
                    inQSearch
                    || moveCount == 1
                    || (
                        // late move reductions
                        moveCount <= 5
                        || depth <= 2
                        || alpha < (score = -AlphaBeta(depth - moveCount / LMRMoves - depth / LMRDepth - (pvNode ? 1 : 2), -alpha - 1, -alpha))
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
