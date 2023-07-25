using ChessChallenge.API;
using NUnit.Framework.Internal;

namespace Tests;

[TestFixture]
public class MateSearchTests
{
    private const int SearchTimeMultiplier = 30;
    private const string TestSuiteFolderPath = @"..\..\..\testsuites\";

    [TestCaseSource(nameof(GetTestCases))]
    public void ShouldFindMateInX(string fen, int mateIn, int searchTimeMs)
    {
        var bot = new MyBot();
        var board = Board.CreateBoardFromFEN(fen);

        for (var moveIndex = 0; moveIndex < mateIn * 2 - 1; moveIndex++)
        {
            var move = bot.Think(board, new ChessChallenge.API.Timer(searchTimeMs * SearchTimeMultiplier));
            board.MakeMove(move);
        }

        Assert.That(board.IsInCheckmate(), Is.True, $"Expected a checkmate in {mateIn} moves for FEN: '{fen}'.");
    }

    private static IEnumerable<TestCaseData> GetTestCases()
    {
        var tests = new List<(string fileName, int mateIn, int searchForMs, string displayName)>
        {
            ("mate1", mateIn: 1, searchForMs: 50, "Mate in One"),
            ("mate2", mateIn: 2, searchForMs: 50, "Mate in Two"),
            ("mate3", mateIn: 3, searchForMs: 75, "Mate in Three"),
            ("mate4", mateIn: 4, searchForMs: 250, "Mate in Four"),
            ("mate5", mateIn: 5, searchForMs: 750, "Mate in Five"),
        };

        foreach (var (fileName, mateIn, searchForMs, displayName) in tests)
        {
            var fens = File.ReadAllLines(Path.Combine(TestSuiteFolderPath, $"{fileName}.txt"));
            foreach (var fen in fens)
            {
                yield return new TestCaseData(fen, mateIn, searchForMs).SetName($"{displayName} - {fen}");
            }
        }
    }
}
