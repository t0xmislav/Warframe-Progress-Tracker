using System;
using System.Collections.Generic;
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
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for NodeCard.xaml
    /// </summary>
    public partial class NodeCard : UserControl
    {
        public NodeCard()
        {
            InitializeComponent();
        }
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("NodeCard Loaded");
            if (DataContext is not Node node)
                return;

            var parent = FindParent<ListBox>(this);
            if (parent?.Tag is not User currentUser)
                { 

                return; 
            }

            var progress = await Task.Run(() => DbService.GetProgressForNode(currentUser, node));

            ClearedNormalCheck.IsChecked = progress.ClearedNormal;
            ClearedSteelCheck.IsChecked = progress.ClearedSteelPath;

            NormalDateBlock.Text = progress.ClearedNormal
                ? $"Normal cleared: {progress.DateNormalClear?.ToShortDateString()}"
                : string.Empty;

            SteelDateBlock.Text = progress.ClearedSteelPath
                ? $"Steel Path: {progress.DateSteelPathClear?.ToShortDateString()}"
                : string.Empty;

            ClearedNormalCheck.Checked += async (_, _) => await UpdateProgressAsync(currentUser, node.Id);
            ClearedNormalCheck.Unchecked += async (_, _) => await UpdateProgressAsync(currentUser, node.Id);
            ClearedSteelCheck.Checked += async (_, _) => await UpdateProgressAsync(currentUser, node.Id);
            ClearedSteelCheck.Unchecked += async (_, _) => await UpdateProgressAsync(currentUser, node.Id);
        }

        private async Task UpdateProgressAsync(User user, int nodeId)
        {
            var normalCleared = ClearedNormalCheck.IsChecked == true;
            var steelPathCleared = ClearedSteelCheck.IsChecked == true;
            await Task.Run(() =>
            {
                DbService.UpdateNodeProgress(
                    user.Id,
                    nodeId,
                    normalCleared,
                    steelPathCleared
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
