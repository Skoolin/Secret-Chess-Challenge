using System;

public class Statistics
{
    private readonly int[] cutoffIdx = new int[512];
    private readonly int[] alphaIdx = new int[512];

    private int cutoffCount = 0;
    private int pvCount = 0;
    private int alphaCount = 0;

    private int QsearchTTProbe = 0;
    private int QsearchTTHit = 0;
    private int ABsearchTTProbe = 0;
    private int ABsearchTTHit = 0;

    public int Nodes { get; set; }

    public void TraceTTProbe(bool inQSearch, ulong zobrist, ulong TTzobrist)
    {
        if (inQSearch)
        {
            QsearchTTProbe++;
            if (TTzobrist == zobrist)
                QsearchTTHit++;
        }
        else
        {
            ABsearchTTProbe++;
            if (TTzobrist == zobrist)
                ABsearchTTHit++;
        }
    }

    public void TracePVOrAllNodes(int TTnodeType, int latestAlpha)
    {
        if (TTnodeType == 1)
        {
            alphaIdx[latestAlpha]++;
            pvCount++;
        }
        else if (TTnodeType == 3)
        {
            alphaCount++;
        }
    }

    public void TraceCutoffs(int moveCount)
    {
        cutoffCount++;
        cutoffIdx[moveCount]++;
    }

    public void PrintStatistics()
    {
        Console.WriteLine("qsearch TT hit rate: " + (100d * QsearchTTHit / QsearchTTProbe).ToString("0.##\\%"));
        Console.WriteLine("AB search TT hit rate: " + (100d * ABsearchTTHit / ABsearchTTProbe).ToString("0.##\\%"));

        Console.WriteLine("pv: " + (100d * pvCount / (cutoffCount + pvCount + alphaCount)).ToString("0.##\\%"));
        Console.WriteLine("alpha cut: " + (100d * alphaCount / (cutoffCount + pvCount + alphaCount)).ToString("0.##\\%"));
        Console.WriteLine("beta cut: " + (100d * cutoffCount / (cutoffCount + pvCount + alphaCount)).ToString("0.##\\%"));

        Console.WriteLine("beta cutoff accuracy:");
        for (int i = 0; i < 20; i++)
            Console.WriteLine("" + i + ": " + (100d * cutoffIdx[i] / cutoffCount).ToString("0.##\\%"));

        Console.WriteLine("pv move accuracy:");
        for (int i = 0; i < 20; i++)
            Console.WriteLine("" + i + ": " + (100d * alphaIdx[i] / pvCount).ToString("0.##\\%"));
    }
}
