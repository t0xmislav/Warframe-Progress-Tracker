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
    class CodexViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Model.CodexEntry> FilteredEntries { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public User CurrentUser { get; }
        public ICollectionView ItemsView { get; }

        private string _selectedCategory;

        private const int _batchSize = 50;
        private int _offset = 0;
        private bool _isLoading = false;
        private bool _hasMore = true;

        public string SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _nameFilter;
        public string NameFilter
        {
            get => _nameFilter;
            set { _nameFilter = value; OnPropertyChanged(); ApplyFilters(); }
        }


        private List<Model.Item> _allItemsSummaries = new();
        private List<Model.Node> _allNodeSummaries = new();
        private List<Model.CodexEntry> _allSummaries = new();
        private List<Model.CodexEntry> _filteredEntries = new();

        public CodexViewModel(Model.User currentUser)
        {
            CurrentUser = currentUser;
            _ = InitializeAsync();
            
        }
        public async Task InitializeAsync()
        {
            await LoadCodexSummariesAsync();
            LoadCategories();
            await LoadNextBatchAsync();
        }
        public async Task LoadNextBatchAsync()
        {
            Task.Yield();
            if (_isLoading || !_hasMore) return;
            _isLoading = true;
            try
            {
                var filtered = _allSummaries
                    .Where(x => (SelectedCategory == "All" || x.Category.DisplayName == SelectedCategory) &&
                        (string.IsNullOrWhiteSpace(NameFilter) || x.Name.Contains(NameFilter, StringComparison.OrdinalIgnoreCase)))
                    .Skip(_offset)
                    .Take(_batchSize)
                    .ToList();
                if (filtered.Count == 0) 
                {
                    _hasMore = false;
                    return; 
                }
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var entry in filtered)
                    {
                        FilteredEntries.Add(entry);
                    }
                });
                    
                _offset += filtered.Count();
                _hasMore = filtered.Count == _batchSize;
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
                .ToList();

            categories.Insert(0, "All");
            Categories.Clear();
            foreach (var c in categories) Categories.Add(c);

            SelectedCategory = "All";
        }
        private void ApplyFilters()
        {
            var allSummaries = new List<CodexEntry>();
            allSummaries.AddRange(_allItemsSummaries);
            allSummaries.AddRange(_allNodeSummaries);

            var filtered = allSummaries
                .Where(e =>
                    (SelectedCategory == "All" || e.Category.DisplayName == SelectedCategory) &&
                    (string.IsNullOrWhiteSpace(NameFilter) || e.Name.Contains(NameFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            FilteredEntries.Clear();
            foreach (var entry in filtered.Take(_batchSize))
                FilteredEntries.Add(entry);

            _offset = filtered.Take(_batchSize).Count();
            _hasMore = filtered.Count() > _batchSize;
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
