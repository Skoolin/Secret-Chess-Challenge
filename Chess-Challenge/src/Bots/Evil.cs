using ChessChallenge.API;
using System;
using System.Linq;

// A simple bot that can spot mate in one, and always captures the most valuable piece it can.
// Plays randomly otherwise.
public class Evil : IChessBot
{
    public Move Think(Board board, Timer timer) =>
        board.GetLegalMoves().MaxBy(x =>
        {
            board.MakeMove(x);
            var cm = board.IsInCheckmate();
            board.UndoMove(x);
            return cm ? 100000 : Math.Abs(x.StartSquare.Index - board.GetKingSquare(board.IsWhiteToMove).Index) + 50 * (int)x.CapturePieceType;
        });
}