
using System;

namespace ChessChallenge.API
{
    public interface IChessBot
    {
        Move Think(Board board, int maxDepth)
        {
            throw new NotImplementedException("this bot did not implement depth limited search!");
        }

        Move Think(Board board, Timer timer);
    }
}
