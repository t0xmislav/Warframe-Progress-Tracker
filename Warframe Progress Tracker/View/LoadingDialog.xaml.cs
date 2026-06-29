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
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for LoadingDialog.xaml
    /// </summary>
    public partial class LoadingDialog : Window
    {
        private bool _allowClose = false;
        private string _defaultMessageKey = "LoadingTextStr";
        private bool _usingDefaultMessage = true;
        private string _currentMessageKey;
        private object[] _currentMessageArgs;
        public event Action? OnCancel;
        public LoadingDialog(string message = "Loading, please wait...")
        {
            InitializeComponent();
            UpdateMessage(message, new object[] {});
            Closing += LoadingDialog_Closing;
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        private void LoadingDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e) 
        {
            if(!_allowClose) 
                e.Cancel = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            UpdateMessage("CancellingStr");
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
        private void SetDefaultMessage()
        {
            string text = (string)Application.Current.Resources[_defaultMessageKey];
            UpdateMessage(text);
            _usingDefaultMessage = true;
        }
        private void OnLanguageChanged()
        {
            if(_currentMessageKey != null)
            {
                Dispatcher.Invoke(() =>
                {
                    var text = string.Format((string)Application.Current.Resources[_currentMessageKey], _currentMessageArgs);
                    MessageBlock.Text = text;
                });
            }
        }
        public void UpdateMessage(string resourceKey, params object[] args)
        {
            _currentMessageArgs = args;
            _currentMessageKey = resourceKey;
            Dispatcher.Invoke(() =>
            {
                if (resourceKey == "DownloadingImageProgressOnlyStr")
                {
                    var res = (string)Application.Current.Resources[resourceKey];
                    // args[0] is already an int percentage (0-100)
                    if (args[0] is int percentage)
                    {
                        MainProgressBar.IsIndeterminate = false;
                        MainProgressBar.Maximum = 100;
                        MainProgressBar.Value = percentage;
                        MessageBlock.Text = string.Format(res, $"{percentage}%");
                    }
                    else
                    {
                        MainProgressBar.IsIndeterminate = true;
                        MessageBlock.Text = args[0]?.ToString() ?? string.Empty;
                    }
                    return;
                }

                MainProgressBar.IsIndeterminate = true;
                try
                {
                    var res = (string)Application.Current.Resources[resourceKey];
                    MessageBlock.Text = string.Format(res, args);
                }
                catch
                {
                    MessageBlock.Text = args?.Length > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty;
                }
            });
        }
        protected override void OnClosed(EventArgs e)
        {
            LanguageManager.LanguageChanged -= OnLanguageChanged;
            base.OnClosed(e);
        }

        public void UpdateProgress(double fraction)
        {
            Dispatcher.Invoke(() =>
            {
                fraction = Math.Max(0.0, Math.Min(1.0, fraction));
                MainProgressBar.IsIndeterminate = false;
                MainProgressBar.Value = fraction;
            });
        }
    }
}
