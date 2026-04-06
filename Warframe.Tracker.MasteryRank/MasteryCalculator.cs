using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe.Tracker.MasteryRank
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

            if (rank <= MaxNormalRank)
            {
                return PerRankMultiplier * (long)rank * rank;
            }
            int legendaryRank = rank - MaxNormalRank;

            return LegendaryBase + (LegendaryPerRank * legendaryRank);
        }

        public static long CumulativePointsForRank(int rank)
        {
            if (rank <= 0) return 0;
            long total = 0;
            if (rank <= MaxNormalRank)
            {
                total = PerRankMultiplier * (rank * rank);
                return total;
            }

            int legendaryRank = rank - MaxNormalRank;

            total = LegendaryBase + (LegendaryPerRank * legendaryRank);
            return total;

        }
        public static (int Rank, long PointsIntoRank, long PointsForNextRank, double Percent) GetRankFromPoints(long totalPoints)
        {
            if (totalPoints <= 0) return (0, 0, PointsForRank(1), 0.0);

            int rank = 0;
            long cumulative = 0;
            long pointsForNextRank = 0;
            long pointsForCurrentRank = 0;
            if (totalPoints >= LegendaryBase)
            {
                rank = (int)(totalPoints / LegendaryPerRank) + MaxNormalRank;
            }
            else
            {
                rank = (int)Math.Sqrt(totalPoints / PerRankMultiplier);
            }
            pointsForCurrentRank = PointsForRank(rank);
            pointsForNextRank = PointsForRank(rank + 1);
            long pointsIntoRank = totalPoints - pointsForCurrentRank;
            double percent = pointsForNextRank > pointsForCurrentRank
                ? (double)pointsIntoRank / (pointsForNextRank - pointsForCurrentRank) * 100
                : 0.0;
            return (rank, pointsIntoRank, pointsForNextRank, percent);
        }
    }
}
