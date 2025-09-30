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
    class DashboardViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Model.ItemWithProgress> FilteredItems { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        public ICollectionView ItemsView { get; }

        private string _selectedCategory;

        private const int _batchSize = 50;
        private int _offset = 0;
        private bool _isLoading = false;
        private bool _hasMore = true;
        public UserProgress UserProgress { get; set; }
        public bool Owned
        {
            get => UserProgress?.Owned ?? false;
            set
            {
                if (UserProgress != null) return;
                if (UserProgress.Owned != value) { 
                    UserProgress.Owned = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool Mastered
        {
            get => UserProgress?.Mastered ?? false;
            set
            {
                if (UserProgress != null) return;
                if (UserProgress.Owned != value)
                {
                    UserProgress.Owned = value;
                    OnPropertyChanged();
                }
            }
        }
        public DateTime? DateMastered
        {
            get => UserProgress?.DateMastered;
            set
            {
                if (UserProgress != null) return;
                if (UserProgress.DateMastered != value)
                {
                    UserProgress.DateMastered = value;
                    OnPropertyChanged();
                }
            }
        }
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
        private List<Model.Item> _filteredSummaries = new();
        private List<Model.ItemWithProgress> _allItemsProgress = new();
        private User _currentUser;
        public DashboardViewModel(Model.User currentUser)
        {
            _currentUser = currentUser;
            _allItemsSummaries = DbService.GetAllItems();
            LoadCategories();
            
        }
        public async Task LoadNextBatchAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                var batchSummaries = _filteredSummaries.Skip(_offset).Take(_batchSize).ToList();
                if (batchSummaries.Count == 0)
                {
                    _hasMore = false;
                    _isLoading = false;
                    return;
                }


                var batch = await Task.Run(() =>
                {
                    return batchSummaries.Select(summary =>
                    {
                        var progress = DbService.GetProgressForItem(_currentUser.Id, summary.Id);
                        var vm = new ItemWithProgress { Item = summary, UserProgress = progress };
                        vm.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(ItemWithProgress.Owned))
                            {
                                Debug.WriteLine("SAVING Owned");
                                DbService.SetOwned(_currentUser.Id, summary.Id, vm.Owned);
                            }
                            if (e.PropertyName == nameof(ItemWithProgress.Mastered))
                            {
                                Debug.WriteLine("SAVING MASTERED");
                                DbService.SetMastered(_currentUser.Id, progress.ItemId, vm.Mastered);
                            }
                        };
                        return vm;
                    }).ToList();
                });
                foreach (var vm in batch)
                {
                    FilteredItems.Add(vm);
                    _allItemsProgress.Add(vm);
                }
                _offset += batchSummaries.Count();
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
            _filteredSummaries = _allItemsSummaries
                .Where(i =>
                    (SelectedCategory == "All" || i.Category?.DisplayName == SelectedCategory) &&
                    (string.IsNullOrWhiteSpace(NameFilter) || i.Name.Contains(NameFilter, StringComparison.OrdinalIgnoreCase))
                ).ToList();

            FilteredItems.Clear();
            _offset = 0;
            _hasMore = true;
            _ = LoadNextBatchAsync();
        }
        private void MasteredCheckBox(object sender, EventArgs e)
        {

        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
