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
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;

namespace Warframe_Progress_Tracker.ViewModel
{
    public class CodexViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Model.CodexEntry> FilteredEntries { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public User CurrentUser { get; }
        public ICollectionView ItemsView { get; }

        private const int BatchSize = 50;

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


        private List<Model.Item> _allItemsSummaries = new();
        private List<Model.Node> _allNodeSummaries = new();
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
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    RefreshFilteredEntries(resetOffset : false));
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
                Item item => Utils.ProgressCacheUtil.GetItemProgress(CurrentUser.Id, item.Id)?.DateMastered ?? DateTime.MinValue,
                Node node => Utils.ProgressCacheUtil.GetNodeProgress(CurrentUser.Id, node.Id)?.DateSteelPathClear > DateTime.MinValue
                            ? Utils.ProgressCacheUtil.GetNodeProgress(CurrentUser.Id, node.Id)?.DateSteelPathClear ?? DateTime.MinValue
                            : Utils.ProgressCacheUtil.GetNodeProgress(CurrentUser.Id, node.Id)?.DateNormalClear ?? DateTime.MinValue,
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
            }
            var filtered = ApplyFilters(_allSummaries).ToList();
            var sorted = ApplySorting(filtered).ToList();
            

            var batch = sorted.Skip(_offset).Take(BatchSize).ToList();

            if (!preserveLoaded) FilteredEntries.Clear();

            foreach (var entry in batch)
                FilteredEntries.Add(entry);
            
            _offset += batch.Count;
            _hasMore = filtered.Count > _offset;
        }
           
        public async Task LoadCodexSummariesAsync()
        {
            List<Item> items = null;
            List<Node> nodes = null;
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

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
