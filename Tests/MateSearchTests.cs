using ChessChallenge.API;
using NUnit.Framework.Internal;

namespace Tests;

[TestFixture]
public class MateSearchTests
{
    private const int SearchTimeMultiplier = 30;
    private static readonly int[] Timings = { 150, 300, 750, 2750 };

    [TestCaseSource(nameof(GetTestCases))]
    public void ShouldFindMateInX(string fen, int mateIn)
    {
        var bot = new MyBot();
        var board = Board.CreateBoardFromFEN(fen);

        for (var moveIndex = mateIn * 2 - 2; moveIndex >= 0; moveIndex--)
        {
            var move = bot.Think(board, new ChessChallenge.API.Timer(Timings[moveIndex / 2] * SearchTimeMultiplier));
            board.MakeMove(move);
        }

        Assert.That(board.IsInCheckmate(), Is.True, $"Expected a checkmate in {mateIn} moves for FEN: '{fen}'.");
    }

    private static IEnumerable<TestCaseData> GetTestCases()
    {
        var tests = new List<(string fileName, int mateIn, string displayName)>
        {
            ("mate1", mateIn: 1, "Mate in One"),
            ("mate2", mateIn: 2, "Mate in Two"),
            ("mate3", mateIn: 3, "Mate in Three"),
            ("mate4", mateIn: 4, "Mate in Four"),
        };

        foreach (var (fileName, mateIn, displayName) in tests)
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "testsuites", $"{fileName}.txt");
            var fens = File.ReadAllLines(path);

            foreach (var fen in fens)
            {
                yield return new TestCaseData(fen, mateIn).SetName($"{displayName} - {fen}");
            }
        }
    }
}
