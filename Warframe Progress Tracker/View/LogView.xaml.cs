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
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for LogView.xaml
    /// </summary>
    public partial class LogView : UserControl
    {

        public LogView(Model.User user)
        {
            InitializeComponent();
            DataContext = new ViewModel.LogViewModel(user);
        }

        // Save note on Enter key press
        private void AdminNoteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox tb && tb.DataContext is ViewModel.LogRowViewModel vm)
                {
                    var cmd = vm.SaveCommand;
                    if (cmd != null && cmd.CanExecute(null)) cmd.Execute(null);
                    e.Handled = true;
                }
            }
        }

    }
}
