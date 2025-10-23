using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for ItemCard.xaml
    /// </summary>
    public partial class ItemCard : UserControl
    {
        public ItemCard()
        {
            InitializeComponent();
        }
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not Item item)
                return;

            // Find the parent ListBox (or CodexView) and get the bound User from its Tag
            var parent = FindParent<ListBox>(this);
            if (parent?.Tag is not User currentUser)
                return;

            var progress = await Task.Run(() => DbService.GetProgressForItem(currentUser, item));

            OwnedCheck.IsChecked = progress.Owned;
            MasteredCheck.IsChecked = progress.Mastered;
            DatesBlock.Text = progress.Mastered
                ? $"Mastered: {progress.DateMastered?.ToShortDateString()}"
                : string.Empty;
            Utils.ProgressCacheUtil.StoreItemProgress(currentUser.Id, item.Id, progress);
            // Attach event handlers for saving changes
            OwnedCheck.Checked += async (_, _) => await UpdateProgressAsync(currentUser, item);
            OwnedCheck.Unchecked += async (_, _) => await UpdateProgressAsync(currentUser, item);
            MasteredCheck.Checked += async (_, _) => await UpdateProgressAsync(currentUser, item);
            MasteredCheck.Unchecked += async (_, _) => await UpdateProgressAsync(currentUser, item);
            
        }

        private async Task UpdateProgressAsync(User user, Item item)
        {
            bool owned = OwnedCheck.IsChecked == true;
            bool mastered = MasteredCheck.IsChecked == true;

            await Task.Run(() =>
            {
                DbService.UpdateItemProgress(
                    user.Id,
                    item.Id,
                    mastered,
                    owned
                );
            });
            var updated = DbService.GetProgressForItem(user, item);
            Utils.ProgressCacheUtil.StoreItemProgress(user.Id, item.Id, updated);
            if (mastered)
            {
                DatesBlock.Text = $"Mastered: {updated.DateMastered?.ToShortDateString()}";
            }
            else
            {
                DatesBlock.Text = string.Empty;
            }
        }
        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            if(DataContext is Model.Item item)
            {
                var editWindow = new ItemEditWindow(item);
                editWindow.Owner = Application.Current.MainWindow;
                editWindow.ShowDialog();
            }
        }
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if(DataContext is Model.Item item)
            {
                var result = MessageBox.Show($"Delete item '{item.Name}'?", "Confirm Delete", MessageBoxButton.YesNo);
                if(result == MessageBoxResult.Yes)
                {
                    DbService.DeleteItem(item);

                    var parentListBox = FindParent<ListBox>(this);
                    if (parentListBox?.ItemsSource is ObservableCollection<Model.CodexEntry> entries) entries.Remove(item);
                }
            }
        }
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
