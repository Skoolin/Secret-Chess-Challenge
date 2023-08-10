using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.Application.TokenCounter; // #DEBUG
using static ChessChallenge.Application.UCI;          // #DEBUG

public class MyBot : IChessBot
{
    [Tunable] public int TempoBonus { get; set; } = 40; // #DEBUG
    [Tunable] public int RFPMargin { get; set; } = 384; // #DEBUG
    [Tunable] public int FPMargin { get; set; } = 384; // #DEBUG
    [Tunable] public int FPFixedMargin { get; set; } = 240; // #DEBUG
    [Tunable] public int SoftTimeLimit { get; set; } = 35; // #DEBUG
    [Tunable] public int HardTimeLimit { get; set; } = 3; // #DEBUG

    private readonly Statistics stats = new(); // #DEBUG

    // Save received search parameters to simplify function signatures
    private Timer timer;
    private Board board;

    private Move bestMove;

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
12966254764254301516153172300m, 14510052961560982499911946825m, 16990749909760457755565053280m, 17295403938744138873223861640m,
18229894207707195796310024003m, 19764016313451491653552849561m, 19148668275190189959039631718m, 17291781901302500752033398627m,
16371794075443846860248666143m, 17612166115307142820777057577m, 18235971875051130507113882653m, 20107365432196063278080943910m,
19793044700361576634852270361m, 21947355251440122796995342365m, 22559067012390056817827667749m, 21311441288480456117524585485m,
17301458067760031299652049944m, 18538189199817528145652836115m, 20719081934100537891728288026m, 20708206324161704518538850077m,
21330784175952375912631597340m, 22563883863595139278928965920m, 22863711596850198340501139226m, 21307814548342647694295129615m,
16059886565682983654836231189m, 19469062118313025811827941401m, 20399930258746892678164734741m, 22573574178493680977128746267m,
21959444584423030224201410073m, 23503242818962091877196786198m, 22567510604373845294450827542m, 21322321676548493088447619597m,
16374202501147004960275061008m, 18856136690729957774126570264m, 19783382831482907887455527445m, 21953404659388236013412573979m,
22567534271832733236240737559m, 23801838107157153343789425688m, 22259229779341860834650447894m, 21636651834668649425122384396m,
17609724651898809106529671443m, 19148687294344290851317046806m, 20719081952396390508624437785m, 22565106975389314065820180501m,
22566315919727167412772168726m, 23187689586728205557677842452m, 21634215149047561236653499417m, 20703342212388167724831037454m,
16676424492654776347401017104m, 18538184385283434122066548505m, 19776124480052874855059642899m, 22256840317458189966343364626m,
22254417761826325403566890767m, 22862507375161124101841707292m, 21313868659350356128438902555m, 20072306509344147827297573645m,
16059886417644580886665973093m, 16990768835852465175535441502m, 18232335560713680201946320988m, 19770089332607408588147283541m,
18229908356635434616555656832m, 21629403020657349092680948555m, 19157159145342751865451599751m, 17603661133229535044170499968m,
    }.SelectMany(decimal.GetBits).SelectMany(BitConverter.GetBytes).ToArray();

    // these 4 can be in the psqt without additional token cost!!
    byte[] AdjacentBitboard = { 0b00000010, 0b00000101, 0b00001010, 0b00010100, 0b00101000, 0b01010000, 0b10100000, 0b01000000 }; // #DEBUG
    int[] mgQueensidePawns = { 165, 173,  75, 106, 102, 119, 159, 130,
28, 44, 37, 64, 49,  7,  5, 11,
23, 18, 25, 33, 27, 23, 32, 11,
20, 21, 15, 21, 26, 27, 22, 12,
15, 24, 20, 25, 20, 18, 19, 10,
22, 30, 22, 16, 19, 18, 17,  9,
22, 36, 28, 26,  9, 18, 16, 11,
  9,   4,   6,   6, 150,  98,  79,  79,     };
    int[] mgKingsidePawns = {  66,  72, 162,  73, 114, 104,  71,  87,
44, 43, 41, 43, 49, 29, 24,  7,
19, 26, 33, 31, 32, 41, 19, 16,
18, 22, 21, 24, 27, 27, 20, 18,
15, 19, 19, 23, 23, 24, 21, 17,
15, 18, 19, 19, 22, 18, 28, 24,
15, 19, 16, 13, 19, 28, 33, 23,
 71, 118, 158, 126, 111, 124,  91, 120,    };
    int[] egQueensidePawns = {   68,  93, 142,  71, 128,  69, 168, 141,
66, 63, 55, 50, 47, 63, 65, 64,
55, 56, 47, 43, 35, 36, 40, 43,
36, 35, 28, 28, 22, 20, 24, 27,
28, 26, 22, 22, 21, 20, 23, 23,
24, 21, 20, 24, 21, 21, 23, 22,
25, 21, 19, 19, 25, 24, 26, 26,
  9,   1,   1,   2, 161, 105, 108,  90,  };
    int[] egKingsidePawns = { 132,  90, 172,  88, 116,  83,  86, 156,
58, 56, 49, 44, 42, 49, 57, 60,
44, 42, 38, 35, 32, 29, 41, 45,
29, 26, 23, 21, 18, 20, 25, 26,
24, 23, 20, 19, 18, 17, 19, 20,
23, 23, 19, 22, 21, 20, 19, 18,
25, 25, 24, 25, 25, 21, 18, 17,
157, 127,  93, 145,  90,  69, 103,  90,    };
    int[] MgPassedRankBonus = { 4, 0, 0, 0, 2, 0, 15, 3, }, // #DEBUG
    EgPassedRankBonus = { 3, 1, 3, 7, 13, 26, 29, -2, }, // #DEBUG
    MGConnectedRankBonus = { 2, 1, 4, 4, 5, 9, 57, 0, }, // #DEBUG
    EGConnectedRankBonus = { 4, -1, 2, 2, 4, 4, -12, 1 },
    MgKingSurrounded = { 87, 91, 96, 98, 95, 95, 95, 95 },
    EgKingSurrounded = { 51, 53, 52, 49, 49, 49, 49, 49 };
    int MgBishopPair = 9, MgHalfRook = 4, MgFullRook = 6, MgOpenKing = 6, EgBishopPair = 9, EgHalfRook = 1, EgFullRook = 1, EgOpenKing = 2;
    // p12 stat significant

    int MgIsolatedBonus = 0;
    int EgIsolatedBonus = 0;

    (int middleGame, int endGame) EvaluatePawn(int square, ulong enemyPawnBoard, ulong ourPawnBoard, bool white)
    {
        int middleGame = 0, endGame = 0;

        int file = square & 0b111;
        int rank = square >> 3;

        int rankFromPlayerPerspective = white ? rank : 7 - rank;

        // helpful masks
        ulong fileMask = 0x0101010101010101UL << file;
        ulong adjacentMask = 0x0101010101010101UL * AdjacentBitboard[file];

        /******************
         * passed pawn
         ******************/
        ulong forwardMask = white
            ? 0xFFFFFFFFFFFFFFFFUL << (8 * (rankFromPlayerPerspective + 1))
            : 0xFFFFFFFFFFFFFFFFUL >> (8 * (rankFromPlayerPerspective + 1));
        ulong passedMask =
            forwardMask // all squares forward of pawn
            & (fileMask | adjacentMask); // that on the same or neighboring files

        if ((enemyPawnBoard & passedMask) == 0) // passedPawn!
        {
            middleGame += MgPassedRankBonus[rankFromPlayerPerspective];
            endGame += EgPassedRankBonus[rankFromPlayerPerspective];
        }

        /******************
         * isolated pawn
         ******************/
        if ((ourPawnBoard & adjacentMask) == 0) // isolated pawn!
        {
            middleGame -= MgIsolatedBonus;
            endGame -= EgIsolatedBonus;
        }

        /******************
         * connected pawns
         ******************/
        ulong rankMask
            = (0b11111111UL << (rank * 8)) // phalanx pawns
            | (0b11111111UL << ((white ? -8 : 8) + rank * 8)); // supporting pawns
        ulong connectedMask = rankMask & adjacentMask;

        if ((ourPawnBoard & connectedMask) != 0) // connected!
        {
            middleGame += MGConnectedRankBonus[rankFromPlayerPerspective];
            endGame += EGConnectedRankBonus[rankFromPlayerPerspective];
        }

        return (middleGame, endGame);
    }

    (int middleGame, int endGame) EvaluatePiece(int piece, int square, ulong enemyPawnBoard, ulong ourPawnBoard, bool white)
    {
        int middleGame = 0, endGame = 0;

        int file = square & 0b111;
        // int rank = square >> 3;

        // int rankFromPlayerPerspective = white ? rank : 7 - rank;

        // helpful masks
        ulong fileMask = 0x0101010101010101UL << file;

        /******************
         * rook on open file
         ******************/
        if (piece == 3) // rook
        {
            if ((fileMask & ourPawnBoard) == 0) // semi open file
            {
                middleGame += MgHalfRook;
                endGame += EgHalfRook;
            }
            if ((fileMask & (ourPawnBoard | enemyPawnBoard)) == 0) // open file
            {
                middleGame += MgFullRook;
                endGame += EgFullRook;
            }
        }
        /******************
         * king on open file
         ******************/
        if (piece == 5) // king
        {
            // int surrounded = System.Numerics.BitOperations.PopCount(BitboardHelper.GetKingAttacks(new(square)) & ourPawnBoard);
            // middleGame += MgKingSurrounded[surrounded];
            // endGame += EgKingSurrounded[surrounded];
            if ((fileMask & ourPawnBoard) == 0)
            {
                // open king 
                middleGame -= MgOpenKing;
                endGame -= EgOpenKing;
            }
        }

        return (middleGame, endGame);
    }

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
            ulong enemyPawnBoard = board.GetPieceBitboard(PieceType.Pawn, xor is 0);
            ulong ourPawnBoard = board.GetPieceBitboard(PieceType.Pawn, xor is 56);
            ulong ourKing = board.GetPieceBitboard(PieceType.King, xor is 56);
            int bishops = 0;
            for (var piece = 0; piece < 6; piece++)
            {
                ulong bitboard = board.GetPieceBitboard((PieceType)piece + 1, xor is 56);
                while (bitboard != 0)
                {
                    int square = BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard);
                    if (piece == 0)
                    {
                        if ((ourKing & 506381209866536711) > 0)
                        {
                            // Queenside pawns
                            mgScore += mgQueensidePawns[square ^ xor];
                            egScore += egQueensidePawns[square ^ xor];
                            continue;
                        }
                        if ((ourKing & 16204198715729174752) > 0)
                        {
                            // Kingside pawns
                            mgScore += mgKingsidePawns[square ^ xor];
                            egScore += egKingsidePawns[square ^ xor];
                            continue;
                        }
                    }
                    if (piece == 2)
                    {
                        bishops++;
                    }
                    int index = piece + // piece index
                        16 * (square ^ xor);    // row of square

                    var (middleGame, endGame) = piece == 0
                        ? (0, 0)
                        : EvaluatePiece(piece, square, enemyPawnBoard, ourPawnBoard, xor is 56);
                    // var (middleGame, endGame) = piece == 0 ? EvaluatePawn(square, enemyPawnBoard, ourPawnBoard, xor is 56) : (0, 0);

                    mgScore += pieceSquareTables[index] + middleGame;
                    egScore += pieceSquareTables[index + 6] + endGame;

                    // Save 8 tokens by packing a lookup table into a single int
                    phase += 0b_0100_0010_0001_0001_0000 >> 4 * piece & 0xF;
                }
            }
            if (bishops == 2)
            {
                mgScore += MgBishopPair;
                egScore += EgBishopPair;
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
          : move.IsPromotion && move.PromotionPieceType is PieceType.Queen ? 1
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
            if (!root && board.IsRepeatedPosition() || board.FiftyMoveCounter >= 100)
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
            score = -AlphaBeta(depth - 3 - depth / 6, -beta, -beta + 1, false);
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
                || alpha < (score = -AlphaBeta(depth - (int)(pvNode ? 2 : 1 + Math.Log2(depth)), -alpha - 1, -alpha)))

                // zero window search
                if (root
                || moveCount > 1
                || alpha < (score = -AlphaBeta(depth - 1, -alpha - 1, -alpha))
                && score < beta)

                    // full window search
                    score = -AlphaBeta(depth - 1, -beta, -alpha);

            board.UndoMove(m);

            // Terminate search if time is up
            if (HardTimeLimit * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining) return 0;

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

        bestMove = default;

        int alpha = -100_000_000,
            beta = 100_000_000;

        for (int depth = 1; timer.MillisecondsElapsedThisTurn * SoftTimeLimit < timer.MillisecondsRemaining && depth < 64;)
        {
            var score = AlphaBeta(depth, alpha, beta, true, true);

            if (alpha >= score || score >= beta)
            {
                alpha = -100_000_000;
                beta = 100_000_000;
                continue;
            }

            alpha = score - 300;
            beta = score + 300;

            // Search was terminated at root as it was a repeated position or a 50 move draw
            if (bestMove == default) break; // #DEBUG
            SendReport(depth, score);       // #DEBUG

            // stats.PrintStatistics(); // #DEBUG

            depth++;
        }

        return bestMove;
    }
}