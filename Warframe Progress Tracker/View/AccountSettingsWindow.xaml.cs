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
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for AccountSettingsWindow.xaml
    /// </summary>
    public partial class AccountSettingsWindow : Window
    {
        private readonly Model.User _user;
        public AccountSettingsWindow(Model.User user)
        {
            InitializeComponent();
            _user = user;

            DisplayNameBox.Text = _user.WarframeDisplayName ?? "";
            PlatformBox.SelectedItem = _user.Platform ?? "pc";
            LanguageBox.SelectedIndex = 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(string.Format((string)System.Windows.Application.Current.Resources["SaveConfirmationStr"]),
                string.Format((string)System.Windows.Application.Current.Resources["SaveSettingsStr"]), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            string displayName = DisplayNameBox.Text.Trim();
            string platform = ((ComboBoxItem)PlatformBox.SelectedItem).Content.ToString();
            string lang = ((ComboBoxItem)LanguageBox.SelectedItem).Tag.ToString();

            AuthService.LinkWarframeAccount(_user.Id, displayName, platform);
            LanguageManager.SetLanguage(lang);
            MessageBox.Show(string.Format((string)System.Windows.Application.Current.Resources["SettingsUpdatedStr"]));
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(string.Format((string)System.Windows.Application.Current.Resources["CancelSettingsStr"]),
                string.Format((string)System.Windows.Application.Current.Resources["CancelStr"]), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            this.Close();
        }
    }
}
