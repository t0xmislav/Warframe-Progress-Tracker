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
                ? string.Format((string)Application.Current.Resources["MasteredDateStr"], progress.DateMastered?.ToShortDateString())
                : string.Empty;
            Utils.ProgressCacheUtil.StoreItemProgress(currentUser.Id, item.Id, progress);
            // Attach event handlers for saving changes
            OwnedCheck.Checked +=  (_, _) => UpdateProgressAsync(currentUser, item);
            OwnedCheck.Unchecked +=  (_, _) => UpdateProgressAsync(currentUser, item);
            MasteredCheck.Checked +=  (_, _) => UpdateProgressAsync(currentUser, item);
            MasteredCheck.Unchecked +=  (_, _) => UpdateProgressAsync(currentUser, item);
            
        }

        private void UpdateProgressAsync(User user, Item item)
        {
            bool owned = OwnedCheck.IsChecked == true;
            bool mastered = MasteredCheck.IsChecked == true;

            ThreadPoolManager.QueueDatabaseWrite(async () =>
            {
                DbService.UpdateItemProgress(
                    user.Id,
                    item.Id,
                    mastered,
                    owned
                );
              
                var updated = DbService.GetProgressForItem(user, item);
                Utils.ProgressCacheUtil.StoreItemProgress(user.Id, item.Id, updated);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                        if (mastered)
                        {
                            DatesBlock.Text = string.Format((string)Application.Current.Resources["MasteredDateStr"], updated.DateMastered?.ToShortDateString());
                        }   
                        else
                        {
                            DatesBlock.Text = string.Empty;
                        }
                });
            });
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
                var result = MessageBox.Show(string.Format((string)Application.Current.Resources["DeleteItemStr"], item.Name), (string)Application.Current.Resources["ConfirmDeleteStr"], MessageBoxButton.YesNo);
                if(result == MessageBoxResult.Yes)
                {
                    ThreadPoolManager.QueueDatabaseWrite(async () => {
                        DbService.DeleteItem(item);
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show((string)Application.Current.Resources["DeleteItemSuccessStr"], (string)Application.Current.Resources["SuccessStr"], MessageBoxButton.OK, MessageBoxImage.Information);
                            var parentListBox = FindParent<ListBox>(this);
                            if (parentListBox?.ItemsSource is ObservableCollection<Model.CodexEntry> entries) entries.Remove(item);
                        });
                    });
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
