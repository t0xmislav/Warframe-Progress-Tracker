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
using Warframe_Progress_Tracker.Services;
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
            _vm = new ViewModel.CodexViewModel(currentUser);
            DataContext = _vm;
            Loaded += async (s, e) =>
            {
                await _vm.LoadNextBatchAsync();
            };
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

            if (sv == null) {
                return; 
            }

            const double threshold = 250.0;
            if (sv.VerticalOffset + sv.ViewportHeight >= sv.ExtentHeight - threshold) 
            {
                await _vm.LoadNextBatchAsync();
            }
        }

        
    }
}
