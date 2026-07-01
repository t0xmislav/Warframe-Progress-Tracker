using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Warframe.Tracker.MasteryRank;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.ViewModel
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CategoryProgress> CategoryProgresses { get; } = new();
        public double TotalProgressPercentage { get; private set; }
        public string TotalProgressPercentageText { get; private set; }
        public string UserDisplayName {  get; private set; }
        public string TotalRankText { get; private set; }
        private readonly User _currentUser;
        private BitmapImage? _masteryRankImage;
        public BitmapImage? MasteryRankImage
        {
            get => _masteryRankImage;
            private set
            {
                if (_masteryRankImage == value) return;
                _masteryRankImage = value;
                OnPropertyChanged();
            }
        }
        private readonly object _totalMasteredLock = new();

        private CancellationTokenSource _cts = new();

        private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(5);
        public DashboardViewModel(User currentUser) 
        {
            _currentUser = currentUser;
            if(_currentUser.WarframeDisplayName is not null) 
            {
                UserDisplayName = _currentUser.WarframeDisplayName;
            }
            else
            {
                UserDisplayName = _currentUser.Name;
            }
            _ = PeriodicRefreshLoop(_cts.Token);
        }

        private async Task PeriodicRefreshLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await RefreshDasboardProgressAsync();
                    
                    await Task.Delay(_refreshInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LoggerService.Log("Error in periodic refresh loop", ex.ToString());
                Console.WriteLine($"Error in periodic refresh loop: {ex.Message}");
            }
        }

        public void Stop()
        {
            _cts.Cancel();
        }
        private async Task RefreshDasboardProgressAsync()
        {
            var categories = DbService.GetCategories().Where(c => c.DisplayName != "Node").ToList();
            var nodes = DbService.GetAllNodes();
            int totalMasterdEntries = 0;
            int totalMasteryPoints = 0;

            var categoryProgressResults = new CategoryProgress[categories.Count()];
            var tasks = categories.Select((category, index) => Task.Run(() => {
                var items = DbService.GetItemByCategory(category);
                int masteredInCategory = items.Count(i => ProgressCacheUtil.GetItemProgress(_currentUser.Id, i.Id)?.Mastered == true);
                var points = items.Where(i => ProgressCacheUtil.GetItemProgress(_currentUser.Id, i.Id)?.Mastered == true)
                    .Sum(i => i.MasteryPoints);
                lock (_totalMasteredLock)
                {
                    totalMasterdEntries += masteredInCategory;
                    totalMasteryPoints += points;
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
            int nodeCount = nodes.Count();
            tasks = tasks.Concat([
                Task.Run(() =>
                {
                    int normalCleared = nodes.Count(n => ProgressCacheUtil.GetNodeProgress(_currentUser.Id, n.Id)?.ClearedNormal == true);
                    var points = nodes.Where(n => ProgressCacheUtil.GetNodeProgress(_currentUser.Id, n.Id)?.ClearedNormal == true)
                        .Sum(n => n.MasteryPoints);
                    lock (_totalMasteredLock)
                    {
                        totalMasterdEntries += normalCleared;
                        totalMasteryPoints += points;
                    }

                    normalNodeProgress = new CategoryProgress
                    {
                        Category = new Category{DisplayName = "Normal Nodes"},
                        MasteredItems = normalCleared,
                        TotalItems = nodeCount,
                    };
                }),
                Task.Run(() =>
                {
                    int spCleared = nodes.Count(n => ProgressCacheUtil.GetNodeProgress(_currentUser.Id, n.Id)?.ClearedSteelPath == true);
                    var points = nodes.Where(n => ProgressCacheUtil.GetNodeProgress(_currentUser.Id, n.Id)?.ClearedSteelPath == true)
                        .Sum(n => n.MasteryPoints);
                    lock (_totalMasteredLock)
                    {
                        totalMasterdEntries += spCleared;
                        totalMasteryPoints += points;
                    }

                    steelPathNodeProgress = new CategoryProgress
                    {
                        Category = new Category{ DisplayName = "Steel Path Nodes" },
                        MasteredItems = spCleared,
                        TotalItems = nodeCount,
                    };
                })
            ]);
            /*
            var sw = Stopwatch.StartNew();
            foreach(var task in tasks) await task;
            sw.Stop();
            Debug.WriteLine($"Sequential progress calculation took {sw.ElapsedMilliseconds}ms");
            */
            
            var sw2 = Stopwatch.StartNew();
            await Task.WhenAll(tasks);
            sw2.Stop();
            Debug.WriteLine($"Parallel progress calculation took {sw2.ElapsedMilliseconds}ms");
            //LoggerService.Log("Perf", $"Parallel progress calculation took {sw2.ElapsedMilliseconds}ms");


            Application.Current.Dispatcher.Invoke(() =>
            {
                var rankInfo = MasteryCalculator.GetRankFromPoints(totalMasteryPoints);
                TotalRankText = $"Current Rank: {rankInfo.Rank}";

                MasteryRankImage = MasteryResourceLoader.GetMasteryRankImage(rankInfo.Rank);

                CategoryProgresses.Clear();
                foreach (var cp in categoryProgressResults) CategoryProgresses.Add(cp);

                if(normalNodeProgress is not null) CategoryProgresses.Add(normalNodeProgress);
                if(steelPathNodeProgress is not null) CategoryProgresses.Add(steelPathNodeProgress);

                UserDisplayName = DbService.GetUserById(_currentUser.Id).WarframeDisplayName ?? _currentUser.Name;

                TotalProgressPercentage = CategoryProgresses.Sum(c => c.TotalItems) > 0
                ? (double)totalMasterdEntries / CategoryProgresses.Sum(c => c.TotalItems) : 0;
                TotalProgressPercentageText = $"{TotalProgressPercentage*100:F2}%";
                OnPropertyChanged(nameof(TotalProgressPercentageText));
                OnPropertyChanged(nameof(TotalRankText));
                OnPropertyChanged(nameof(UserDisplayName));
                OnPropertyChanged(nameof(TotalProgressPercentage));
            });
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
