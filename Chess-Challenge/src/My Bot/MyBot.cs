using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.Application.TokenCounter; // #DEBUG

public class MyBot : IChessBot
{
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
        15527718067412796676516028191m,  8399820470846585704624306477m, 10558904983445930289162953245m,  9021156174086055237355905315m,
         9631658972036358145130045208m, 18974390005662380829299384095m, 29510324511571103937848232516m, 27029618489058859208830708835m,
        26099883476052627820773790544m, 24546437483537291803033556566m, 25155750263434007170989379133m, 24561010928081676011306569811m,
        30441174039747988990160101205m, 27653395970016243911816208223m, 27345124497484642808770550616m, 26093862588129788440751332441m,
        38214699823327146449669814133m, 35430553068521796122038271102m, 34174412825030593294642474090m, 32318735451399985839018634608m,
        35415993881940662000821103462m, 71149996979409615102958596210m, 68973873559652857502892807383m, 68353723002718324983356319714m,
        68351290928784660402227828698m, 68352514114009781948374834395m,  3735675282024917413893167324m,  2793893923079760280449648644m,
         3110618302058680515738670093m,  1557124901050238459467008521m,   634761890586530898263084044m,  5594951508362538592247024646m,
        18957488173582742186895745570m, 15844390409499181291069979711m,  9943585538283079101501941544m,  9944780259892458300874301472m,
        10563755001757796499686892324m, 15855375004247389420887613986m, 22680974922243682527814501954m, 21133578392886980392134067527m,
        22988060988844575815769933380m, 19269386352174283210287695689m, 22679780273986402983218462017m, 22372722598443776123607664969m,
        23614308304869543800157653323m, 22682226535057168957132459085m, 22990478932933979712642632264m, 22062023922059957114117769547m,
        37283841183038821028027004792m, 36350536246303389444958811765m, 36663652755949497217092974198m, 35422071735139996125443159156m,
        36352958783342426620169647220m, 64001509549863180327317828468m, 65858405256792297366147419847m, 64942072837885930542608601556m,
        63074220778828506326067237063m, 60895741173669013883185843916m,  3417671085017541293638075844m,  5285532631127781169936994829m,
         6213968678893860001783484945m,  4043951684321988802480707092m,  5590134566071035545873616395m,  1869056152482561563936296978m,
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
        return 96 + (mgScore * phase + egScore * (24 - phase)) * (board.IsWhiteToMove ? 1 : -1);
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
            if (!board.IsInCheck() && depth < 8 && beta <= eval - 384 * depth)
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
                && eval + 384 * depth + 240 < alpha // threshhold of 50 + 100 * depth centipawns
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
            if (10 * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining) return 0;

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

        for (int depth = 1; timer.MillisecondsElapsedThisTurn * 35 < timer.MillisecondsRemaining && depth < 64;)
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