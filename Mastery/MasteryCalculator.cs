using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe.Tracker.Mastery
{
    public static class MasteryCalculator
    {
        public const int MaxNormalRank = 30;
        private const int PerRankMultiplier = 2500;
        private const long LegendaryBase = 2250000;
        private const long LegendaryPerRank = 147500;

        public static long PointsForRank(int rank)
        {
            if (rank <= 0) return 0;

            if(rank <= MaxNormalRank)
            {
                return PerRankMultiplier * (long)rank * rank;
            }
            int legendaryIndex = rank - MaxNormalRank;

            return LegendaryBase + LegendaryPerRank * (legendaryIndex - 1);
        }

        public static long CumulativePointsForRank(int rank)
        {
            if (rank <= 0) return 0;
            long total = 0;
            if(rank <= MaxNormalRank)
            {
                long n = rank;
                long sumSquares = n * (n + 1) * (2 * n + 1) / 6;
                total = PerRankMultiplier * sumSquares;
                return total;
            }

            long sumSquares30 = MaxNormalRank * (MaxNormalRank + 1) * (2 * MaxNormalRank + 1) / 6;
            total = PerRankMultiplier * sumSquares30;

            int legendaryCount = rank - MaxNormalRank;

            for(int i = 1; i < legendaryCount; i++)
            {
                total += LegendaryBase + LegendaryPerRank * (i - 1);
            }
            return total;

        }
        public static(int Rank, long PointsIntoRank, long PointsForNextRank, double Percent) GetRankFromPoints(long totalPoints)
        {
            if(totalPoints <= 0) return (0, 0, PointsForRank(1), 0.0);

            int rank = 0;
            long cumulative = 0;

            while (true)
            {
                int nextRank = rank + 1;
                long pointsForNextRank = PointsForRank(nextRank);
                long nextCumulative = cumulative + pointsForNextRank;
                if (totalPoints < nextCumulative)
                {
                    long pointsIntoRank = totalPoints - cumulative;
                    double percent = pointsForNextRank == 0 ? 1.0 : (double)pointsIntoRank / pointsForNextRank;
                    return (rank, pointsIntoRank, pointsForNextRank, percent);

                }
                rank = nextRank;
                cumulative = nextCumulative;
                if(rank > 50)
                {
                    //safety break
                    throw new InvalidOperationException("Rank calculation exceeded reasonable limits.");
                }
            }
            return (rank, 0, PointsForRank(rank +1), 0.0);
        }
    }
}
