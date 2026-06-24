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
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;
using Warframe_Progress_Tracker.ViewModel;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for ItemCard.xaml
    /// </summary>
    public partial class ItemCard : UserControl
    {
        private User _currentUser;
        public ItemCard()
        {
            
            InitializeComponent();
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {

            if (DataContext is not Item item)
                return;

            // Find the parent ListBox (or CodexView) and get the bound User from its Tag
            var parent = FindParent<ListBox>(this);
            if (parent?.Tag is not User currentUser)
                return;

            _currentUser = currentUser;
            var progress = item.getItemProgress(currentUser) ?? new ItemProgress {User = currentUser, Item = item };

            OwnedCheck.IsChecked = progress.Owned;
            MasteredCheck.IsChecked = progress.Mastered;
            DatesBlock.Text = progress.Mastered
                ? string.Format((string)Application.Current.Resources["MasteredDateStr"], progress.DateMastered?.ToShortDateString())
                : string.Empty;
            Utils.ProgressCacheUtil.StoreItemProgress(currentUser.Id, item.Id, progress);
            // Attach event handlers for saving changes
            OwnedCheck.Checked +=  (_, _) => UpdateProgressAsync(currentUser, item);
            OwnedCheck.Unchecked +=  (_, _) => UpdateProgressAsync(currentUser, item);
            MasteredCheck.Checked +=  (_, _) => UpdateProgressAsync(currentUser, item);
            MasteredCheck.Unchecked +=  (_, _) => UpdateProgressAsync(currentUser, item);
            
        }

        private async void UpdateProgressAsync(User user, Item item)
        {
            bool owned = OwnedCheck.IsChecked == true;
            bool mastered = MasteredCheck.IsChecked == true;

            try
            {
                var updated = await item.UpdateProgressAsync(user, owned, mastered);

                if (mastered)
                {
                    DatesBlock.Text = string.Format((string)Application.Current.Resources["MasteredDateStr"], updated.DateMastered?.ToShortDateString());
                }
                else
                {
                    DatesBlock.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Log("UpdateProgressFailed", $"Failed to update progress for {item.Name}: {ex.Message}");
                MessageBox.Show((string)Application.Current.Resources["UpdateProgressErrorStr"], (string)Application.Current.Resources["ErrorStr"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            if(DataContext is Model.Item item)
            {
                var editWindow = new ItemEditWindow(item, _currentUser);
                editWindow.Owner = Application.Current.MainWindow;
                editWindow.ShowDialog();
            }
        }
        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if(DataContext is Model.Item item)
            {
                var result = MessageBox.Show(string.Format((string)Application.Current.Resources["DeleteItemStr"], item.Name), (string)Application.Current.Resources["ConfirmDeleteStr"], MessageBoxButton.YesNo);
                if(result == MessageBoxResult.Yes)
                {
                    await Task.Run(() => {
                        DbService.DeleteItem(item);

                        LoggerService.Log("Deleted Item", $"{_currentUser.Name}: Deleted item: {item.Name}.");
                    });

                    var parentListBox = FindParent<ListBox>(this);
                    if (parentListBox?.ItemsSource is ObservableCollection<Model.CodexEntry> entries) entries.Remove(item);

                    var codexVm = parentListBox?.DataContext as CodexViewModel
                        ?? FindParent<System.Windows.Window>(this)?.DataContext as CodexViewModel;
                    codexVm?.RemoveEntry(item);
                    MessageBox.Show((string)Application.Current.Resources["DeleteItemSuccessStr"], (string)Application.Current.Resources["SuccessStr"], MessageBoxButton.OK, MessageBoxImage.Information);
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
