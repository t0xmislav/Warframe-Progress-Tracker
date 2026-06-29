using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.ViewModel
{
    public class CodexViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CodexEntry> FilteredEntries { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public User CurrentUser { get; }

        private const int BatchSize = 25;

        private int _offset = 0;
        private bool _isLoading = false;
        private bool _hasMore = true;
        private string _sortKey = "Name";
        public string SortKey
        {
            get => _sortKey;
            set { 
                if(_sortKey == value) return;
                _sortKey = value; 
                OnPropertyChanged(); 
                _ = RecomputeAndRefreshAsync(resetOffset : true, CancellationToken.None); 
            }
        }
        private string _selectedCategory;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { 
                if(_selectedCategory == value) return;
                _selectedCategory = value; 
                OnPropertyChanged(); 
                _ = RecomputeAndRefreshAsync(resetOffset : true, CancellationToken.None); 
            }
        }

        private string _nameFilter;
        public string NameFilter
        {
            get => _nameFilter;
            set {
                if (_nameFilter == value) return;
                _nameFilter = value; 
                OnPropertyChanged(); 

                DebounceFilter();
            }
        }
        private string _clearFilter = "All";
        public string ClearFilter
        {
            get => _clearFilter;
            set
            {
                if (_clearFilter == value) return;
                _clearFilter = value;
                OnPropertyChanged();

                _ = RecomputeAndRefreshAsync(resetOffset: true, token: CancellationToken.None);
            }
        }
        private List<CodexEntry> _sortedCache = new();

        private List<CodexEntry> _allSummaries = new();
        private readonly object _cacheLock = new();

        private CancellationTokenSource? _filterCts;
        private readonly TimeSpan _filterDebounceDelay = TimeSpan.FromMilliseconds(300);

        public CodexViewModel(User currentUser)
        {
            CurrentUser = currentUser;
            _ = InitializeAsync();
        }
        public async Task InitializeAsync()
        {
            SelectedCategory = "All";
            ClearFilter = "All";

            await LoadCodexSummariesAsync();
            
            LoadCategories();
            await RecomputeAndRefreshAsync(resetOffset: true, token: CancellationToken.None);
        }
        public async Task LoadNextBatchAsync()
        {
            if (_isLoading || !_hasMore) return;
            _isLoading = true;

            try
            {
                Debug.WriteLine("Trying refresh...");
                List<CodexEntry> snapshotSorted;
                int startOffset;
                lock (_cacheLock)
                {
                    snapshotSorted = _sortedCache.ToList();
                    startOffset = _offset;
                }

                var batch = snapshotSorted.Skip(startOffset).Take(BatchSize).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var entry in batch)
                        FilteredEntries.Add(entry);
                    lock(_cacheLock)
                    {
                        _offset += batch.Count;
                        _hasMore = snapshotSorted.Count > _offset;
                    }
                });
            }
            finally
            {
                _isLoading = false;
            }
        }
        private void LoadCategories()
        {
            var categories = DbService.GetCategories()
                .Select(c => c.DisplayName ?? "Unknown")
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            categories.Insert(0, "All");
            Categories.Clear();
            foreach (var c in categories) Categories.Add(c);

            SelectedCategory = "All";
        }
        private IEnumerable<CodexEntry> ApplyFilters(IEnumerable<CodexEntry> entries)
        {
            var nameFilter = NameFilter;
            var clearFilter = ClearFilter;
            return entries.Where(e =>
                    (SelectedCategory == "All" || e.Category.DisplayName == SelectedCategory) &&
                    (string.IsNullOrWhiteSpace(nameFilter) || e.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)) &&
                    (clearFilter == "All" || (clearFilter =="Cleared" ? IsCleared(e) : !IsCleared(e)))
                ).ToList();
        }
        private DateTime GetMasteryDate(CodexEntry entry)
        {
            return entry switch
            {
                Item item => ProgressCacheUtil.GetItemProgress(CurrentUser.Id, item.Id)?.GetProgressDate() ?? DateTime.MinValue,
                Node node => ProgressCacheUtil.GetNodeProgress(CurrentUser.Id, node.Id)?.GetClearedDate() ?? DateTime.MinValue,
                            _ => DateTime.MinValue
            };
        }
        private IEnumerable<CodexEntry> ApplySorting(IEnumerable<CodexEntry> entries)
        {
            var key = SortKey;
            return key switch
            {
                "DateMastered" => entries.OrderByDescending(GetMasteryDate),
                _ => entries.OrderBy(e => e.Name)
            };
        }
        
        private void DebounceFilter()
        {
            _filterCts?.Cancel();
            _filterCts?.Dispose();

            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_filterDebounceDelay, token);
                    if (token.IsCancellationRequested) return;

                    await RecomputeAndRefreshAsync(resetOffset: true, token);
                }
                catch (TaskCanceledException) { }
                catch(Exception ex)
                {
                    LoggerService.Log("Error in debounce", $"An error occurred during debounce: {ex}");
                    Debug.WriteLine($"Error in debounce: {ex}");
                }
            }, CancellationToken.None);
        }

        private async Task RecomputeAndRefreshAsync(bool resetOffset, CancellationToken token)
        {
            List<CodexEntry> snapshotAll;
            lock (_cacheLock)
            {
                snapshotAll = _allSummaries.ToList();
            }


            var sorted = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var filtered = ApplyFilters(snapshotAll).ToList();
                return ApplySorting(filtered).ToList();
            }, token);

            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_cacheLock)
                {
                    _sortedCache = sorted;
                    _offset = 0;
                    _hasMore = _sortedCache.Count > 0;
                }

                FilteredEntries.Clear();
                var batch = _sortedCache.Take(BatchSize).ToList();
                foreach (var entry in batch)
                    FilteredEntries.Add(entry);

                _offset = batch.Count;
                _hasMore = _sortedCache.Count > _offset;
            });
        }
        public async Task LoadCodexSummariesAsync()
        {
            List<Item>? items = null;
            List<Node>? nodes = null;
            await Task.Run(() =>
            {
                var result = DbService.GetAllCodexSummaries();
                items = result.items;
                nodes = result.nodes;
                lock (_cacheLock)
                {
                    _allSummaries.Clear();
                    if (items is not null) _allSummaries.AddRange(items);
                    if (nodes is not null) _allSummaries.AddRange(nodes);
                }

            });
        }
        private bool IsCleared(CodexEntry entry)
        {
            if (entry is Item it)
            {
                return it.IsMastered(CurrentUser);
            }
            if (entry is Node nd)
            {
                return nd.IsNormalCleared(CurrentUser) || nd.IsSpCleared(CurrentUser);
            }
            return false;
        }

        public void RemoveEntry(CodexEntry entry)
        {
            lock (_cacheLock)
            {
                _allSummaries.Remove(entry);
                _sortedCache.Remove(entry);
            }
            FilteredEntries.Remove(entry);
        }
        public void ReplaceEntry(CodexEntry oldEntry, CodexEntry newEntry)
        {
            lock (_cacheLock)
            {
                var index = _allSummaries.IndexOf(oldEntry);
                if (index >= 0)
                {
                    _allSummaries[index] = newEntry;
                }
                index = _sortedCache.IndexOf(oldEntry);
                if (index >= 0)
                {
                    _sortedCache[index] = newEntry;
                }
            }
            var filteredIndex = FilteredEntries.IndexOf(oldEntry);
            if (filteredIndex >= 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FilteredEntries[filteredIndex] = newEntry;
                });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
