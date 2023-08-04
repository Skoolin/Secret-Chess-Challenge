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
    // Node counter for debugging purposes
    private int nodes;   // #DEBUG

    // Save received search parameters to simplify function signatures
    private Timer timer;
    private Board board;

    private Move bestMove;

    // Can save 4 tokens by removing this line and replacing `TABLE_SIZE` with a literal
    private const ulong TABLE_SIZE = 1 << 22;

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
    private readonly (Move, Move)[] killerMoves = new (Move, Move)[1024]; // MAX_GAME_LENGTH = 1024

    private readonly Move[] badQuiets = new Move[512];

    private readonly byte[] pieceSquareTables = new[] {
          533633171124025748709646864m,  3016762064087496223854506256m,  3945226575538272208856562704m,  3946430778775224576385300752m,
         4255911084676820992275009296m,  6111612199338538186227855376m,  5180739299284134186378215440m,  3945212351873611197867044368m,
         4245030733935754572036518948m,  6111616940449853658337393451m,  5804549818944205519967310108m,  6116447902984889960854080035m,
         6120060531719816126932142110m,  7350756423709970175496572713m,  6423515079327201742935573527m,  5797286745075916844593790990m,
         5792451097564692430738242063m,  6107980699662968159791501073m,  6418674672628912767852694037m,  6118861069363635909582475286m,
         6427137153220688228215968797m,  7972134924257571508285824283m,  7659023118388215430106862101m,  5799695225979419569800367116m,
         4559346743179704281170723085m,  6421092561014132857595643411m,  6422310931854381760411092753m,  6736617477854170488478322196m,
         6739035347939862358445279765m,  7044893561711786869562298642m,  6739035310831330627236284179m,  5186774539639109229717505803m,
         3935541020257423147191975947m,  4874876419207379348953449488m,  6421097302042123026337318415m,  6427137227364288890384827922m,
         6732985959509474728395884819m,  6424709893954489462016395025m,  5497459012459803759472232210m,  4254683251088647046556107019m,
         3936740482824715111628357131m,  4861573475931645237713977615m,  5800904170817172415177311503m,  6417461061403846407021675790m,
         6418665283231384270824752913m,  6110384439753231859933136401m,  5490205457181550945860601879m,  4560536742565587721502015502m,
         3315352592808387497276554505m,  4243807604185686246041597968m,  5171058485680933820519170316m,  5793655301301257410055848459m,
         5793645875015321621826781965m,  5172257948176158304037062421m,  4860355068197069264520167448m,  3931904723917235458543271692m,
         1765504933017786962740915728m,  3004663397907760433330667280m,  3624847010756598329743128336m,  4238976678685424287341624336m,
         3320188314820977155799138320m,  4241380326259664662758309136m,  3626056010002903460997054224m,  2383256618496987179765743120m,
    }.SelectMany(decimal.GetBits).SelectMany(BitConverter.GetBytes).ToArray();

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

    private int AlphaBeta(int depth, int alpha, int beta, bool nullMoveAllowed = true, bool root = false)
    {
        nodes++; // #DEBUG

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
            AlphaBeta(depth - 2, alpha, beta, nullMoveAllowed, root);

        var (TTzobrist, TTdepth, TTeval, TTtype, TTm) = transpositionTable[TTidx];

        // The TT entry is from a different position, so no best move is available
        if (TTzobrist != zobrist)
            TTm = default;
        else if (!root && TTdepth >= depth && (TTtype is 1 || TTtype is 2 && TTeval >= beta || TTtype is 3 && TTeval <= alpha))
            return TTeval;

        int TTnodeType = 3,
            moveCount = -1,
            score,
            badQuietCount = 1; // starting index 1

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

        foreach (Move m in moves)
        {
            moveCount++;
            // futility pruning:
            // if static eval is far below alpha and this move doesn't seem likely to raise it, 
            // this and later moves probably won't.
            if (!root
                && depth < 8
                && moveCount > 0 // don't prune TT move
                && eval + FPMargin * depth + FPFixedMargin < alpha // threshhold of 50 + 100 * depth centipawns
                && !m.IsCapture
                && !m.IsPromotion)
                break;

            board.MakeMove(m);

            // late move reduction
            if (depth <= 2
                || moveCount <= 4
                || alpha < (score = -AlphaBeta(depth - (int)(pvNode ? 2 : 1 + Math.Log2(depth)), -alpha - 1, -alpha)))

                // zero window search
                if (root
                || moveCount > 0
                || alpha < (score = -AlphaBeta(depth - 1, -alpha - 1, -alpha))
                && score < beta)

                    // full window search
                    score = -AlphaBeta(depth - 1, -beta, -alpha);

            board.UndoMove(m);

            // Terminate search if time is up
            if (HardTimeLimit * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining) return 0;

            if (score > alpha)
            {
                TTnodeType = 1; // PV node
                alpha = score;
                TTm = m;

                if (root)
                    bestMove = TTm;

                if (score >= beta)
                {
                    TTnodeType = 2; // Fail high
                    if (!m.IsCapture)
                    {
                        while (badQuietCount > 0) // starting index 1
                            historyTable[(int)badQuiets[badQuietCount].MovePieceType, badQuiets[badQuietCount--].TargetSquare.Index] -= depth * depth;
                        historyTable[(int)m.MovePieceType, m.TargetSquare.Index] += depth * depth;
                        killerMoves[board.PlyCount] = (m, killerMoves[board.PlyCount].Item1);
                    }
                    break;
                }
            }
            else if (!m.IsCapture)
                badQuiets[badQuietCount++] = m;
        }

        if (!inQSearch)
        {
            // Checkmate or stalemate
            if (moveCount < 0)
                return board.IsInCheck() ? -20_000_000 + board.PlyCount : 0;

            transpositionTable[TTidx] = (zobrist, depth, alpha, TTnodeType, TTm);
        }

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
        Console.Write($"info depth {depth} score cp {(5 * score) / 24} nodes {nodes}");
        Console.Write($" time {timer.MillisecondsElapsedThisTurn}");
        Console.WriteLine($" pv{GetPV(bestMove, 15)}");
    }

    public Move Think(Board _board, Timer _timer)
    {
        timer = _timer;
        board = _board;

        nodes = 0;  // #DEBUG

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

            depth++;
        }

        return bestMove;
    }
}