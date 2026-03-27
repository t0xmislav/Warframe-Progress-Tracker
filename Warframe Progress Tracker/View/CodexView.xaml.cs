using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Warframe_Progress_Tracker.ViewModel;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for CodexView.xaml
    /// </summary>
    public partial class CodexView : UserControl
    {
        private readonly CodexViewModel _vm;

        public CodexView(Model.User currentUser)
        {
            InitializeComponent();
            _vm = new CodexViewModel(currentUser);
            DataContext = _vm;

            this.Loaded += CodexView_Loaded;
        }
        private void CodexView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is CodexViewModel vm)
                {
                    foreach (var rb in FindVisualChildren<RadioButton>(this))
                    {
                        if (rb.Tag is string sortKey && sortKey == vm.SortKey)
                        {
                            rb.IsChecked = true;
                            break;
                        }
                        if (rb.Tag is string clearTag && clearTag == vm.ClearFilter)
                        {
                            rb.IsChecked = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting initial sort radio button: {ex.Message}");
            }
        }
        private void SortEntries_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Tag is string sortKey && DataContext is CodexViewModel vm)
            {
                vm.SortKey = sortKey;
            }
        }
        private async void ItemsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var sv = e.OriginalSource as ScrollViewer;
            Debug.WriteLine("Scroll Called");
            if (sv == null) {
                return;
            }

            const double threshold = 250.0;
            if (sv.VerticalOffset + sv.ViewportHeight >= sv.ExtentHeight - threshold)
            {
                Debug.WriteLine("Scroll Called Load");
                await _vm.LoadNextBatchAsync();
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T t)
                    yield return t;
                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }
    }
}
