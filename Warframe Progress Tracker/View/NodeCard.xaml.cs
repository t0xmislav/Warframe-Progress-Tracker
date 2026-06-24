using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Media;

using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;
using Warframe_Progress_Tracker.ViewModel;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for NodeCard.xaml
    /// </summary>
    public partial class NodeCard : UserControl
    {
        private User _currentUser;
        public NodeCard()
        {
            InitializeComponent();
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {

            if (DataContext is not Node node)
                return;

            var parent = FindParent<ListBox>(this);
            if (parent?.Tag is not User user)
                return;
            _currentUser = user;

            var progress = node.GetNodeProgress(_currentUser) ?? new NodeProgress { User = user, Node = node };

            Utils.ProgressCacheUtil.StoreNodeProgress(user.Id, node.Id, progress);

            ClearedNormalCheck.IsChecked = progress.ClearedNormal;
            ClearedSteelCheck.IsChecked = progress.ClearedSteelPath;

            NormalDateBlock.Text = progress.ClearedNormal
                ? string.Format((string)Application.Current.Resources["NormalClearedDateStr"], progress.DateNormalClear?.ToShortDateString())
                : string.Empty;

            SteelDateBlock.Text = progress.ClearedSteelPath
                ? string.Format((string)Application.Current.Resources["SPClearedDateStr"], progress.DateSteelPathClear?.ToShortDateString())
                : string.Empty;

            ClearedNormalCheck.Checked += (_, _) => {  UpdateProgressAsync(user, node); };
            ClearedNormalCheck.Unchecked += (_, _) => { UpdateProgressAsync(user, node); };
            ClearedSteelCheck.Checked += (_, _) =>{ UpdateProgressAsync(user, node); };
            ClearedSteelCheck.Unchecked += (_, _) => { UpdateProgressAsync(user, node); };
            
        }

        private async void UpdateProgressAsync(User user, Node node)
        {
            var normalCleared = ClearedNormalCheck.IsChecked == true;
            var steelPathCleared = ClearedSteelCheck.IsChecked == true;

            try
            {
                var updated = await node.UpdateProgressAsync(user, normalCleared, steelPathCleared);

                if (normalCleared)
                {
                    NormalDateBlock.Text = string.Format((string)Application.Current.Resources["NormalClearedDateStr"], updated.DateNormalClear?.ToShortDateString());
                }
                else
                {
                    NormalDateBlock.Text = string.Empty;
                }
                if (steelPathCleared)
                {
                    SteelDateBlock.Text = string.Format((string)Application.Current.Resources["SPClearedDateStr"], updated.DateSteelPathClear?.ToShortDateString());
                }
                else
                {
                    SteelDateBlock.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Log("UpdateNodeProgressFailed", $"Failed to update progress for {node.Name}: {ex.Message}");
                MessageBox.Show((string)Application.Current.Resources["UpdateProgressErrorStr"], (string)Application.Current.Resources["ErrorStr"], MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private void EditNode_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is Model.Node node)
            {
                var editWindow = new NodeEditWindow(node, _currentUser);
                editWindow.Owner = Application.Current.MainWindow;
                editWindow.ShowDialog();
            }
        }
        private async void DeleteNode_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is Model.Node node)
            {
                var result = MessageBox.Show(string.Format((string)Application.Current.Resources["DeleteNodeStr"], node.Name), (string)Application.Current.Resources["ConfirmDeleteStr"], MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    await Task.Run(() => {
                        DbService.DeleteNode(node);
                        LoggerService.Log("Deleted Node", $"{_currentUser.Name}: Deleted node: {node.Name}.");
                    });
                    var parentListBox = FindParent<ListBox>(this);
                    if (parentListBox?.ItemsSource is ObservableCollection<Model.CodexEntry> entries) entries.Remove(node);

                    var codexVm = parentListBox?.DataContext as CodexViewModel
                        ?? FindParent<System.Windows.Window>(this)?.DataContext as CodexViewModel;
                    codexVm?.RemoveEntry(node);
                    MessageBox.Show((string)Application.Current.Resources["DeleteNodeSuccessStr"],
                        (string)Application.Current.Resources["SuccessStr"], MessageBoxButton.OK, MessageBoxImage.Information);
                    
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
