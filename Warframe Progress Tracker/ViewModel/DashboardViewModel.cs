using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;

namespace Warframe_Progress_Tracker.ViewModel
{
    class DashboardViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Model.ItemWithProgress> FilteredItems { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        private string _selectedCategory;
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

        private List<Model.ItemWithProgress> _allItemsProgress = new();
        private User _currentUser;
        public DashboardViewModel(Model.User currentUser)
        {
            _currentUser = currentUser;
            LoadItems(currentUser);
        }

        private void LoadItems(Model.User currentUser)
        {
            _allItemsProgress = DbService.GetItemsWithProgress(currentUser.Id)
                .Select(t => {
                    var vm = new Model.ItemWithProgress { Item = t.item, UserProgress = t.progress };
                    vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(Model.ItemWithProgress.Owned) ||
                        e.PropertyName == nameof(Model.ItemWithProgress.Mastered))
                        {
                            SaveProgress(vm);
                        }
                    };
                    return vm;
                })
                .ToList();
            System.Windows.MessageBox.Show($"Fetched {_allItemsProgress.Count}");
            var categories = _allItemsProgress
                .Select(i => i.Item.Category?.DisplayName ?? "Unknown")
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            categories.Insert(0, "All");
            Categories.Clear();
            foreach (var c in categories) Categories.Add(c);

            SelectedCategory = "All";
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var filtered = _allItemsProgress
                .Where(i =>
                    (SelectedCategory == "All" || i.Item.Category?.DisplayName == SelectedCategory) &&
                    (string.IsNullOrWhiteSpace(NameFilter) || i.Item.Name.Contains(NameFilter, StringComparison.OrdinalIgnoreCase))
                ).ToList();

            FilteredItems.Clear();
            foreach (var i in filtered) FilteredItems.Add(i);
        }
        private void SaveProgress(ItemWithProgress vm)
        {
            if (vm == null) return;

            using var connection = new SqliteConnection($"Data Source={DbService.GetDbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO UserProgress (UserId, ItemId, Owned, Mastered, DateOwned, DateMastered)
                VALUES ($userId, $itemId, $owned, $mastered, $ownedDate, $masteredDate)
                ON CONFLICT(UserId, ItemId) DO UPDATE SET
                    Owned=$owned, Mastered=$mastered,
                    DateOwned=$ownedDate, DateMastered=$masteredDate;
                ";
            cmd.Parameters.AddWithValue("$userId", _currentUser.Id);
            cmd.Parameters.AddWithValue("$itemId", vm.Item.UniqueName);
            cmd.Parameters.AddWithValue("$owned", vm.Owned ? 1 : 0);
            cmd.Parameters.AddWithValue("$mastered", vm.Mastered ? 1 : 0);
            cmd.Parameters.AddWithValue("$ownedDate", vm.Owned ? vm.DateOwned?.ToString("o") : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$masteredDate", vm.Mastered ? vm.DateMastered?.ToString("o") : (object)DBNull.Value);

            cmd.ExecuteNonQuery();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
