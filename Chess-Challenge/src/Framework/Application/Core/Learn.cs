using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.Immutable;

public class Learn
{
    static byte[] AdjacentBitboard = { 0b00000010, 0b00000101, 0b00001010, 0b00010100, 0b00101000, 0b01010000, 0b10100000, 0b01000000 };
    static int arrayLength = 48;
    // public static void CompareStaticEval()
    // {
    //     Stable std = new();
    //     MyBot other = new();
    //     var fen1 = Board.CreateBoardFromFEN("6k1/pp5p/6Q1/8/3N4/3R1K2/1r3PPP/2q5 b - - 0 33");
    //     std.board = fen1;
    //     other.board = fen1;
    //     int stable_eval = std.EvaluateStatically();
    //     int test_eval = other.EvaluateStatically();
    //     Console.WriteLine(stable_eval);
    //     Console.WriteLine(test_eval);
    //     // Debug.Assert(std.EvaluateStatically() == other.EvaluateStatically());
    //     // var fen2 = Board.CreateBoardFromFEN("8/p7/2p5/P2pk2p/4p2P/1P3rP1/1K2RP2/8 b - - 1 37");
    //     // std.board = fen2;
    //     // other.board = fen2;
    //     // Console.WriteLine(std.EvaluateStatically());
    //     // Console.WriteLine(other.EvaluateStatically());
    // }
    // public static void WriteData()
    // {
    //     Book book = new Book("C:/Code/Chess-Challenge/Chess-Challenge/quiet-filtered.epd");
    //     // string fen = "rnbqkbnr/ppp1pppp/8/8/2pP4/6P1/PP2PP1P/RNBQKBNR b KQkq - 0 3";
    //     // Board board = Board.CreateBoardFromFEN(fen);
    //     MyBot bot = new MyBot();
    //     // Console.WriteLine(bot.GetEvalData(board));
    //     int counter = 0;
    //     using StreamWriter outputFile = new StreamWriter("quiet2.csv", false);

    //     outputFile.WriteLine("fen,eval");
    //     foreach (var pos in book.positions)
    //     {
    //         counter++;
    //         Board b = Board.CreateBoardFromFEN(pos.fen);
    //         bot.board = b;
    //         int eval = bot.EvaluateStatically();
    //         outputFile.WriteLine("{0},{1}", pos.fen, eval);
    //         // Console.WriteLine("Eval " + eval);
    //         // Console.WriteLine("White eval " + white_eval);
    //         // Console.WriteLine(pos.score);
    //         // if (counter >= 250000)
    //         // {
    //         //     break;
    //         // }
    //     }
    // }
    public static void WriteEncoding()
    {
        Book book = new Book("C:/Code/Chess-Challenge/Chess-Challenge/quiet-filtered.epd");
        // string fen = "rnbqkbnr/ppp1pppp/8/8/2pP4/6P1/PP2PP1P/RNBQKBNR b KQkq - 0 3";
        // Board board = Board.CreateBoardFromFEN(fen);
        // Console.WriteLine(bot.GetEvalData(board));
        int counter = 0;
        using (StreamWriter outputFile = new StreamWriter("quiet.csv", false))
        {
            outputFile.Write("fen,phase");
            for (int i = 0; i < arrayLength; i++)
            {
                outputFile.Write(",f{0}", i);
            }
            outputFile.WriteLine("");
            foreach (var pos in book.positions)
            {
                counter++;
                Board b = Board.CreateBoardFromFEN(pos.fen);
                string engine_data = GetEvalData(b);
                // int side_eval = b.IsWhiteToMove ? pos.score : -pos.score;
                outputFile.WriteLine("{0},{1}", pos.fen, engine_data);
                // Console.WriteLine("Eval " + eval);
                // Console.WriteLine("White eval " + white_eval);
                // Console.WriteLine(pos.score);
                // if (counter >= 250000)
                // {
                //     break;
                // }
            }
        }
    }
    public static string GetEvalData(Board board)
    {
        int[] array = new int[arrayLength];
        // bool isWhite = board.IsWhiteToMove;
        int idx = 0;
        int phase = 0;
        int whiteBishops = 0;
        int blackBishops = 0;
        ulong whiteKing = board.GetPieceBitboard(PieceType.King, true);
        ulong blackKing = board.GetPieceBitboard(PieceType.King, false);
        foreach (bool isWhite in new[] { true, false })
        {
            for (var kind = PieceType.Pawn; kind <= PieceType.King; kind++)
            {
                ulong enemyPawnBoard = board.GetPieceBitboard(PieceType.Pawn, !isWhite);
                ulong ourPawnBoard = board.GetPieceBitboard(PieceType.Pawn, isWhite);
                int offset = (int)kind - 1;
                foreach (var p in board.GetPieceList(kind, isWhite))
                {
                    phase += 0b_0100_0010_0001_0001_0000 >> 4 * offset & 0xF;
                    int sq = isWhite ? p.Square.Index : p.Square.Index ^ 56; // Vertical flip if black
                    int start = offset * 64 + sq; // Activate neuron with this index
                    if (isWhite)
                    {
                        if (p.IsPawn)
                        {
                            if ((whiteKing & 506381209866536711) > 0)
                            {
                                // Queenside pawns
                                array[idx++] = 384 + sq;
                                continue;
                            }
                            if ((whiteKing & 16204198715729174752) > 0)
                            {
                                // Kingside pawns
                                array[idx++] = 384 + 64 + sq;
                                continue;
                            }
                        }
                        array[idx++] = start;
                    }
                    else
                    {
                        if (p.IsPawn)
                        {
                            if ((blackKing & 506381209866536711) > 0)
                            {
                                // Queenside pawns
                                array[idx++] = -1 * (384 + sq);
                                continue;
                            }
                            if ((blackKing & 16204198715729174752) > 0)
                            {
                                // Kingside pawns
                                array[idx++] = -1 * (384 + 64 + sq);
                                continue;
                            }
                        }
                        array[idx++] = -1 * start; // Encode black piece with a negative index, note 0 is already invalid
                    }
                    if (kind == PieceType.Pawn)
                    {
                        // var subarr = EvaluatePawn(p.Square.Index, enemyPawnBoard, ourPawnBoard, isWhite);
                        // int j = 0;
                        // while (subarr[j] != 0)
                        // {
                        //     array[idx++] = subarr[j++];
                        // }
                    }
                    else
                    {
                        if (kind == PieceType.Bishop)
                        {
                            if (isWhite)
                            {
                                whiteBishops++;
                            }
                            else
                            {
                                blackBishops++;
                            }
                        }
                        var subarr = EvaluatePiece(offset, p.Square.Index, enemyPawnBoard, ourPawnBoard, isWhite);
                        int j = 0;
                        while (subarr[j] != 0)
                        {
                            array[idx++] = subarr[j++];
                        }
                    }
                }
            }
        }
        if (whiteBishops == 2)
        {
            array[idx++] = 384;
        }
        if (blackBishops == 2)
        {
            array[idx++] = -384;
        }
        return String.Format("{0},{1}", phase, string.Join(",", array));
    }
    static int[] EvaluatePawn(int square, ulong enemyPawnBoard, ulong ourPawnBoard, bool white)
    {
        int[] arr = new int[4];
        int idx = 0;
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
            if (white)
            {
                arr[idx++] = rankFromPlayerPerspective;
            }
            else
            {
                arr[idx++] = -1 * rankFromPlayerPerspective;
            }
        }

        /******************
         * isolated pawn
         ******************/
        if ((ourPawnBoard & adjacentMask) == 0) // isolated pawn!
        {
            if (white)
            {
                arr[idx++] = 384;
            }
            else
            {
                arr[idx++] = -384;
            }
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
            if (white)
            {
                arr[idx++] = rankFromPlayerPerspective + 56;
            }
            else
            {
                arr[idx++] = -1 * (rankFromPlayerPerspective + 56);
            }
        }

        return arr;
    }

    static int[] EvaluatePiece(int piece, int square, ulong enemyPawnBoard, ulong ourPawnBoard, bool white)
    {
        int[] arr = new int[10];
        int idx = 0;

        int file = square & 0b111;

        // helpful masks
        ulong fileMask = 0x0101010101010101UL << file;

        /******************
         * rook on open file
         ******************/
        if (piece == 3) // rook
        {
            if ((fileMask & ourPawnBoard) == 0) // semi open file
            {
                if (white)
                {
                    arr[idx++] = 385;
                }
                else
                {
                    arr[idx++] = -385;
                }
            }
            if ((fileMask & (ourPawnBoard | enemyPawnBoard)) == 0) // open file
            {
                if (white)
                {
                    arr[idx++] = 386;
                }
                else
                {
                    arr[idx++] = -386;
                }
            }
        }

        /******************
         * king on open file
         ******************/
        if (piece == 5) // king
        {
            // int surrounded = System.Numerics.BitOperations.PopCount(BitboardHelper.GetKingAttacks(new(square)) & ourPawnBoard);
            // int mul = white ? 1 : -1;
            // arr[idx++] = mul * (56 + surrounded); // 0 is valid here, so we have to use the bottom. Technically 8 could overflow, but it should be harmless
            if ((fileMask & ourPawnBoard) == 0) // open king
            {
                if (white)
                {
                    arr[idx++] = -387; // Mark the opposite since it should be a bonus for the other side
                }
                else
                {
                    arr[idx++] = 387;
                }
            }
        }

        return arr;
    }
}

public class Book
{
    public ImmutableArray<Position> positions;
    public Book(String file)
    {
        var positions = new List<Position>();
        foreach (var line in File.ReadLines(file))
        {
            var split = line.Split(" c9 ");
            int score = 0;
            if (split[1] == "\"0-1\";")
            {
                score = -1;
            }
            else if (split[1] == "\"1-0\";")
            {
                score = 1;
            }
            var position = new Position(split[0], score);
            positions.Add(position);
        }
        this.positions = positions.ToImmutableArray<Position>();
    }
}

public record struct Position(String fen, int score) { }