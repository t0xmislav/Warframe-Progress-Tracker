using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;
using Warframe.Tracker.MasteryRank;

namespace Warframe_Progress_Tracker.ViewModel
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CategoryProgress> CategoryProgresses { get; } = new();
        public double TotalProgressPercentage { get; private set; }
        public string TotalProgressPercentageText { get; private set; }
        public string TotalRankText { get; private set; }
        private readonly User _currentUser;

        private int _totalMastered;
        private readonly object _totalMasteredLock = new();

        private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(10);
        public DashboardViewModel(User currentUser) 
        {
            _currentUser = currentUser;

            _ = PeriodicRefreshLoop();
        }

        private async Task PeriodicRefreshLoop()
        {
            while (true)
            {
                ThreadPoolManager.QueueDatabaseRead(RefreshDasboardProgressAsync);

                await Task.Delay(_refreshInterval);
            }
        }
        private async Task RefreshDasboardProgressAsync()
        {
            var categories = DbService.GetCategories().Where(c => c.DisplayName != "Node");
            var nodes = DbService.GetAllNodes();

            int totalMasterdLocal = 0;
            int totalMasteryPoints = 0;

            var categoryProgressResults = new CategoryProgress[categories.Count()];
            var tasks = categories.Select((category, index) => Task.Run(() => {
                var items = DbService.GetItemByCategory(category);
                int masteredInCategory = items.Count(i => ProgressCacheUtil.GetItemProgress(_currentUser.Id, i.Id)?.Mastered == true);

                lock (_totalMasteredLock)
                {
                    totalMasterdLocal += masteredInCategory;
                    totalMasteryPoints += items.Where(i => ProgressCacheUtil.GetItemProgress(_currentUser.Id, i.Id)?.Mastered == true)
                        .Sum(i => i.MasteryPoints);
                }

                categoryProgressResults[index] = new CategoryProgress
                {
                    Category = category,
                    MasteredItems = masteredInCategory,
                    TotalItems = items.Count,
                };
            }));

            CategoryProgress? normalNodeProgress = null;
            CategoryProgress? steelPathNodeProgress = null;
            tasks = tasks.Concat([
                Task.Run(() =>
                {
                    int normalCleared = nodes.Count(n => ProgressCacheUtil.GetNodeProgress(_currentUser.Id, n.Id)?.ClearedNormal == true);
                    
                    lock (_totalMasteredLock)
                    {
                        totalMasterdLocal += normalCleared;
                        totalMasteryPoints += nodes.Where(n => ProgressCacheUtil.GetNodeProgress(_currentUser.Id, n.Id)?.ClearedNormal == true)
                            .Sum(n => n.MasteryPoints);
                    }

                    normalNodeProgress = new CategoryProgress
                    {
                        Category = new Category{DisplayName = "Normal Nodes"},
                        MasteredItems = normalCleared,
                        TotalItems = nodes.Count(),
                    };
                }),
                Task.Run(() =>
                {
                    int spCleared = nodes.Count(n => ProgressCacheUtil.GetNodeProgress(_currentUser.Id, n.Id)?.ClearedSteelPath == true);

                    lock (_totalMasteredLock)
                    {
                        totalMasterdLocal += spCleared;
                        totalMasteryPoints += nodes.Where(n => ProgressCacheUtil.GetNodeProgress(_currentUser.Id, n.Id)?.ClearedSteelPath == true)
                            .Sum(n => n.MasteryPoints);
                    }

                    steelPathNodeProgress = new CategoryProgress
                    {
                        Category = new Category{ DisplayName = "Steel Path Nodes" },
                        MasteredItems = spCleared,
                        TotalItems = nodes.Count(),
                    };
                })
            ]);

            await Task.WhenAll(tasks);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TotalRankText = $"Current Rank: {MasteryCalculator.GetRankFromPoints(totalMasteryPoints).Rank}";
                CategoryProgresses.Clear();
                foreach (var cp in categoryProgressResults) CategoryProgresses.Add(cp);

                if(normalNodeProgress is not null) CategoryProgresses.Add(normalNodeProgress);
                if(steelPathNodeProgress is not null) CategoryProgresses.Add(steelPathNodeProgress);

                _totalMastered = totalMasterdLocal;
                TotalProgressPercentage = CategoryProgresses.Sum(c => c.TotalItems) > 0
                ? (double)_totalMastered / CategoryProgresses.Sum(c => c.TotalItems) : 0;
                TotalProgressPercentage *= 100;
                TotalProgressPercentageText = $"{TotalProgressPercentage:F2}%";
                OnPropertyChanged(nameof(TotalProgressPercentageText));
                OnPropertyChanged(nameof(TotalRankText));
            });
        }
        /*
        private async Task LoadItemProgressAsync()
        {
            var items = await Task.Run(() => DbService.GetAllItems());
            var totalCount = items.Count;
            var masteredCount = 0;

            var groupedByCategory = items.GroupBy(i => i.Category).ToList();
            
            foreach(var group in groupedByCategory)
            {
                var mastered = group.Count(i => i.IsMastered(_currentUser));

                double ratio = group.Any() ? (double)mastered / group.Count() : 0;
                CategoryProgresses.Add(new CategoryProgress
                {
                    Category = group.Key,
                    TotalItems = totalCount,
                    MasteredItems = mastered,
                });

                masteredCount += mastered;

            }
            TotalProgressPercentageText = totalCount == 0 ? 0 : (double)masteredCount / totalCount;
            OnPropertyChanged(nameof(TotalProgressPercentageText));
        }
        */
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
