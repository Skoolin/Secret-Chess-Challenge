using ChessChallenge.API;
using System;

namespace ChessChallenge.Application;

public class UCI
{
    private const string STARTING_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private const string ENGINE_NAME = "Name";
    private const string ENGINE_AUTHOR = "Author";

    private readonly IChessBot _bot;
    private Board _board;

    public UCI(IChessBot bot)
    {
        _bot = bot;
        _board = Board.CreateBoardFromFEN(STARTING_FEN);
    }

    public void StartUciMessageLoop()
    {
        string? input;
        do
        {
            input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;

            var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                continue;

            switch (tokens[0])
            {
                case "uci":
                    Console.WriteLine($"id name {ENGINE_NAME}");
                    Console.WriteLine($"id author {ENGINE_AUTHOR}");
                    Console.WriteLine("uciok");
                    break;

                case "isready":
                    Console.WriteLine("readyok");
                    break;

                case "ucinewgame":
                    _board = Board.CreateBoardFromFEN(STARTING_FEN);
                    break;

                case "position":
                    ParsePositionCommand(tokens);
                    break;

                case "go":
                    ParseGoCommand(tokens);
                    break;
            }
        } while (input != "quit");
    }

    /// <summary>
    /// Parses the `go` command and sends the best move to the GUI.
    /// </summary>
    private void ParseGoCommand(string[] tokens)
    {
        var main = 0;
        var increment = 0;

        var mainTokenName = _board.IsWhiteToMove ? "wtime" : "btime";
        var incrementTokenName = _board.IsWhiteToMove ? "winc" : "binc";

        for (var i = 1; i < tokens.Length; i++)
        {
            if (tokens[i] == mainTokenName)
            {
                main = int.Parse(tokens[++i]);
            }
            else if (tokens[i] == incrementTokenName)
            {
                increment = int.Parse(tokens[++i]);
            }
        }

        // The `Timer` class stores the remaining time for the current player's move without
        // considering the time increment, so just add it to the main time.
        var timer = new Timer(main + increment);

        var bestMove = _bot.Think(_board, timer);
        var uciMove = GetUciMoveRepresentation(bestMove);

        Console.WriteLine($"bestmove {uciMove}");
    }

    /// <summary>
    /// Parses the `position` command and updates the current board accordingly.
    /// </summary>
    private void ParsePositionCommand(string[] tokens)
    {
        var fen = tokens[1] switch
        {
            "startpos" => STARTING_FEN,
            "fen" => string.Join(' ', tokens[2..8]),
            _ => throw new Exception($"Invalid position command: {tokens[1]}")
        };

        _board = Board.CreateBoardFromFEN(fen);

        var movesIndex = Array.IndexOf(tokens, "moves");
        if (movesIndex == -1)
            return;

        for (var i = movesIndex + 1; i < tokens.Length; i++)
        {
            var move = new Move(tokens[i], _board);
            _board.MakeMove(move);
        }
    }

    /// <summary>
    /// Returns the UCI string representation of the given move.
    /// </summary>
    private string GetUciMoveRepresentation(Move move)
    {
        // API.Move.ToString() returns redundant information (e.g. "Move: 'e2e4'")
        // so just use the underlying Chess.Move struct to only get the UCI string
        return Chess.MoveUtility.GetMoveNameUCI(new Chess.Move(move.RawValue));
    }
}
