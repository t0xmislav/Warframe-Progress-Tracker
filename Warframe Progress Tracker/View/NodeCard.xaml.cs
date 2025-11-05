using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Media;

using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for NodeCard.xaml
    /// </summary>
    public partial class NodeCard : UserControl
    {
        private bool _isInitializing = false; 
        public NodeCard()
        {
            InitializeComponent();
        }
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not Node node)
                return;

            var parent = FindParent<ListBox>(this);
            if (parent?.Tag is not User currentUser)
                return;


            var progress = await Task.Run(() => DbService.GetProgressForNode(currentUser, node));

            Utils.ProgressCacheUtil.StoreNodeProgress(currentUser.Id, node.Id, progress);

            _isInitializing = true;
            ClearedNormalCheck.IsChecked = progress.ClearedNormal;
            ClearedSteelCheck.IsChecked = progress.ClearedSteelPath;
            _isInitializing = false;

            NormalDateBlock.Text = progress.ClearedNormal
                ? $"Normal cleared: {progress.DateNormalClear?.ToShortDateString()}"
                : string.Empty;

            SteelDateBlock.Text = progress.ClearedSteelPath
                ? $"Steel Path: {progress.DateSteelPathClear?.ToShortDateString()}"
                : string.Empty;

            ClearedNormalCheck.Checked += async (_, _) => { if (!_isInitializing) await UpdateProgressAsync(currentUser, node); };
            ClearedNormalCheck.Unchecked += async (_, _) => { if (!_isInitializing) await UpdateProgressAsync(currentUser, node); };
            ClearedSteelCheck.Checked += async (_, _) =>{ if (!_isInitializing) await UpdateProgressAsync(currentUser, node); };
            ClearedSteelCheck.Unchecked += async (_, _) => { if (!_isInitializing) await UpdateProgressAsync(currentUser, node); };
            
        }

        private async Task UpdateProgressAsync(User user, Node node)
        {
            var normalCleared = ClearedNormalCheck.IsChecked == true;
            var steelPathCleared = ClearedSteelCheck.IsChecked == true;
            await Task.Run(() =>
            {
                ThreadPoolManager.QueueDatabaseTask(async () =>
                {
                    DbService.UpdateNodeProgress(
                        user.Id,
                        node.Id,
                        normalCleared,
                        steelPathCleared
                    );
                    var updated = DbService.GetProgressForNode(user, node);
                    Utils.ProgressCacheUtil.StoreNodeProgress(user.Id, node.Id, updated);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (normalCleared)
                        {
                            NormalDateBlock.Text = $"Normal cleared: {updated.DateNormalClear?.ToShortDateString()}";
                        }
                        else
                        {
                            NormalDateBlock.Text = string.Empty;
                        }
                        if (steelPathCleared)
                        {
                            SteelDateBlock.Text = $"Steel path cleared: {updated.DateSteelPathClear?.ToShortDateString()}";
                        }
                        else
                        {
                            SteelDateBlock.Text = string.Empty;
                        }
                    });
                });
            });
        }
        private void EditNode_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is Model.Node node)
            {
                var editWindow = new NodeEditWindow(node);
                editWindow.Owner = Application.Current.MainWindow;
                editWindow.ShowDialog();
            }
        }
        private void DeleteNode_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is Model.Node node)
            {
                var result = MessageBox.Show($"Delete ndoe '{node.Name}'?", "Confirm Delete", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    DbService.DeleteNode(node);
                    var parentListBox = FindParent<ListBox>(this);
                    if (parentListBox?.ItemsSource is ObservableCollection<Model.CodexEntry> entries) entries.Remove(node);
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
