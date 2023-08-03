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
        10867237919514894465131089936m,  3739340322948683493239368222m,  5898424835548028077778014990m,  4360676026188153025970967060m,
         4971178824138455933745106953m, 14313909857764478617914445840m, 24849844363673201726463294261m, 22369138341160956997445770580m,
        21439403328154725609388852289m, 19885957335639389591648618311m, 20495270115536104959604440878m, 19900530780183773799921631556m,
        25780693891850086778775162950m, 22992915822118341700431269968m, 22684644349586740597385612361m, 21433382440231886229366394186m,
        33554219675429244238284875878m, 30770072920623893910653332847m, 29513932677132691083257535835m, 27658255303502083627633696353m,
        30755513734042759789436165207m, 66489516831511712891573657955m, 64313393411754955291507869128m, 63693242854820422771971381459m,
        63690810780886758190842890443m, 63692033966111879736989896140m,  4046373957394001462576269773m,  3104592599606287094541977861m,
         3421316978585207329830999310m,  1867823577576765273559337738m,   945460567113057712355413261m,  5905650184889065406339353863m,
        14297008025684839975510807315m, 11183910261601279079685041456m,  5283105390385176890117003289m,  5284300111994556089489363217m,
         5903274853859894288301954069m, 11194894856349487209502675731m, 18020494774345780316429563699m, 16473098244989078180749129272m,
        18327580840946673604384995125m, 14608906204276380998902757434m, 18019300126088500771833523762m, 17712242450545873912222726714m,
        18953828156971641588772715068m, 18021746387159266745747520830m, 18329998785036077501257694009m, 17401543774162054902732831292m,
        32623361035140918816642066537m, 31690056098405487233573873510m, 32003172608051595005708035943m, 30761591587242093914058220901m,
        31692478635444524408784708965m, 59341029401965278115932890213m, 61197925108894395154762481592m, 60281592689988028331223663301m,
        58413740630930604114682298808m, 56235261025771111671800905661m,  3728369760386625342321178293m,  5596231307654307984029324046m,
         6524667355420386815875814162m,  4354650360848515616573036309m,  5900833242597562359965945612m,  2179754829009088378028626195m,
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