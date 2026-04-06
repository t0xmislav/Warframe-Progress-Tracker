using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Warframe.Tracker.CodexSnapshotPlugin;

namespace CodexSnapshotPlugin
{
    /// <summary>
    /// Interaction logic for LogViewControl.xaml
    /// </summary>
    public partial class LogViewControl : UserControl
    {
        public LogViewControl()
        {
            InitializeComponent();
        }

        // Event handler referenced by the XAML. Signature must match the XAML KeyDown attribute.
        private void AdminNoteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if ((sender as TextBox)?.DataContext is LogRowViewModel vm)
                {
                    if (vm.SaveCommand.CanExecute(null))
                        vm.SaveCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}
