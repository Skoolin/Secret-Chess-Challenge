using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.Immutable;

public class Learn
{
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
            for (int i = 0; i < 32; i++)
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
        int[] array = new int[32];
        // bool isWhite = board.IsWhiteToMove;
        int idx = 0;
        int phase = 0;
        foreach (bool isWhite in new[] { true, false })
        {
            for (var kind = PieceType.Pawn; kind <= PieceType.King; kind++)
            {
                int offset = (int)kind - 1;
                foreach (var p in board.GetPieceList(kind, isWhite))
                {
                    phase += 0b_0100_0010_0001_0001_0000 >> 4 * offset & 0xF;
                    int sq = isWhite ? p.Square.Index : p.Square.Index ^ 56; // Vertical flip if black
                    int start = offset * 64 + sq; // Activate neuron with this index
                    if (isWhite)
                    {
                        array[idx++] = start;
                    }
                    else
                    {
                        array[idx++] = -1 * start; // Encode black piece with a negative index, note 0 is already invalid
                    }
                }
            }
        }
        return String.Format("{0},{1}", phase, string.Join(",", array));
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