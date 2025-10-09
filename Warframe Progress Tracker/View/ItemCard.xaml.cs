using System;
using System.Collections.Generic;
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

            // Attach event handlers for saving changes
            OwnedCheck.Checked += async (_, _) => await UpdateProgressAsync(currentUser, item.Id);
            OwnedCheck.Unchecked += async (_, _) => await UpdateProgressAsync(currentUser, item.Id);
            MasteredCheck.Checked += async (_, _) => await UpdateProgressAsync(currentUser, item.Id);
            MasteredCheck.Unchecked += async (_, _) => await UpdateProgressAsync(currentUser, item.Id);
        }

        private async Task UpdateProgressAsync(User user, int itemId)
        {
            bool owned = OwnedCheck.IsChecked == true;
            bool mastered = MasteredCheck.IsChecked == true;

            await Task.Run(() =>
            {
                DbService.UpdateItemProgress(
                    user.Id,
                    itemId,
                    mastered,
                    owned
                );
            });
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
