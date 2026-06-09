using BenchmarkDotNet.Attributes;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Services;

namespace Warframe_Progress_Tracker.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 1)]
    public class WikiScraperBenchmark
    {
        [Benchmark(Description = "Sequential Scraping")]
        public async Task ScrapeNodesSequential()
        {
            var result = await WikiScraperService.ScrapeNodesAsync(null);
        }

        [Benchmark(Description = "Parallel Scraping (4 concurrent)")]
        public async Task ScrapeNodesParallel()
        {
            var result = await WikiScraperService.ScrapeNodesAsyncParallel(null, default, 4);
        }

        [Benchmark(Description = "Parallel Scraping (8 concurrent)")]
        public async Task ScrapeNodesParallel8()
        {
            var result = await WikiScraperService.ScrapeNodesAsyncParallel(null, default, 8);
        }
    }
}