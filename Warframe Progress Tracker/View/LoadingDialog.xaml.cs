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
using System.Windows.Shapes;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for LoadingDialog.xaml
    /// </summary>
    public partial class LoadingDialog : Window
    {
        private bool _allowClose = false;

        public event Action? OnCancel;
        public LoadingDialog(string message = "Loading, please wait...")
        {
            InitializeComponent();
            UpdateMessage(message);
            Closing += LoadingDialog_Closing;
        }
        private void LoadingDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e) 
        {
            if(!_allowClose) 
                e.Cancel = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            UpdateMessage("Cancelling...");
            OnCancel?.Invoke();
        }
        public void SafeClose()
        {
            Dispatcher.Invoke(() =>
            {
                if (IsVisible)
                {
                    _allowClose = true;
                    Close();
                }
            });
        }

        public void UpdateMessage(string newMessage)
        {
            Dispatcher.Invoke(() => MessageBlock.Text = newMessage);
        }
    }
}
