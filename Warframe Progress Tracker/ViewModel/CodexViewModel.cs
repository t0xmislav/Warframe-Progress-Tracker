using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.View;

namespace Warframe_Progress_Tracker.ViewModel
{
    public class CodexViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CodexEntry> FilteredEntries { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public User CurrentUser { get; }
        public ICollectionView ItemsView { get; }

        private const int BatchSize = 25;

        private string _selectedCategory;
        

        private int _offset = 0;
        private bool _isLoading = false;
        private bool _hasMore = true;
        private string _sortKey = "Name";
        public string SortKey
        {
            get => _sortKey;
            set { _sortKey = value; OnPropertyChanged(); RefreshFilteredEntries(resetOffset : true, preserveLoaded: false); }
        }
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); RefreshFilteredEntries(resetOffset : true, preserveLoaded : false); }
        }

        private string _nameFilter;
        public string NameFilter
        {
            get => _nameFilter;
            set { _nameFilter = value; OnPropertyChanged(); RefreshFilteredEntries(resetOffset : true, preserveLoaded : false); }
        }

        private List<CodexEntry> _filteredCache = new();
        private List<CodexEntry> _sortedCache = new();
        private readonly Dictionary<int, DateTime> _masteryCacheDate = new();

        private List<Model.Item>? _allItemsSummaries = new();
        private List<Model.Node>? _allNodeSummaries = new();
        private List<Model.CodexEntry> _allSummaries = new();

        public CodexViewModel(Model.User currentUser)
        {
            
            CurrentUser = currentUser;
            _ = InitializeAsync();

        }
        public async Task InitializeAsync()
        {
            SelectedCategory = "All";

            
            await LoadCodexSummariesAsync();
            
            LoadCategories();
            RefreshFilteredEntries(resetOffset : true);
        }
        public async Task LoadNextBatchAsync()
        {
            if (_isLoading || !_hasMore) return;
            _isLoading = true;

            try
            {
                Debug.WriteLine("Trying refresh...");
                RefreshFilteredEntries(resetOffset : false);
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
            return entries.Where(e =>
                    (SelectedCategory == "All" || e.Category.DisplayName == SelectedCategory) &&
                    (string.IsNullOrWhiteSpace(NameFilter) || e.Name.Contains(NameFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
        private DateTime GetMasteryDate(CodexEntry entry)
        {
            return entry switch
            {
                Item item => Utils.ProgressCacheUtil.GetItemProgress(CurrentUser.Id, item.Id)?.GetProgressDate() ?? DateTime.MinValue,
                Node node => Utils.ProgressCacheUtil.GetNodeProgress(CurrentUser.Id, node.Id)?.GetClearedDate() ?? DateTime.MinValue,
                            _ => DateTime.MinValue
            };
        }
        private IEnumerable<CodexEntry> ApplySorting(IEnumerable<CodexEntry> entries)
        {
            return SortKey switch
            {
                "DateMastered" => entries.OrderByDescending(GetMasteryDate),
                _ => entries.OrderBy(e => e.Name)
            };
        }
        private void RefreshFilteredEntries(bool resetOffset = false, bool preserveLoaded = true)
        {
            if (resetOffset)
            {
                _offset = 0;
                if (!preserveLoaded) FilteredEntries.Clear();

                _filteredCache = ApplyFilters(_allSummaries).ToList();
                _sortedCache = ApplySorting(_filteredCache).ToList();
            }


            var batch = _sortedCache.Skip(_offset).Take(BatchSize).ToList();

            if (!preserveLoaded) FilteredEntries.Clear();

            foreach (var entry in batch)
                FilteredEntries.Add(entry);
            
            _offset += batch.Count;
            _hasMore = _filteredCache.Count > _offset;
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

            });
            Application.Current.Dispatcher.Invoke(() =>
            {

                _allItemsSummaries = items;
                _allNodeSummaries = nodes;

                _allSummaries.Clear();
                _allSummaries.AddRange(items);
                _allSummaries.AddRange(nodes);
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
