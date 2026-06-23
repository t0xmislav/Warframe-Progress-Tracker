using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using Warframe_Progress_Tracker.Utils.Logger;

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
            if (IniFileService.Exists)
            {
                string savedName = IniFileService.Read("Account", "DisplayName", _user.WarframeDisplayName);
                string savedPlatform = IniFileService.Read("Account", "Platform", _user.Platform);
                string savedLanguage = IniFileService.Read("Account", "Language", "en");
                string savedTheme = IniFileService.Read("Account", "Theme", "Light");
                string savedSpeedLimit = IniFileService.Read("Download", "SpeedLimit", "Unlimited");
                DisplayNameBox.Text = savedName;
                PlatformBox.SelectedItem = PlatformBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Content == savedPlatform);

                LanguageBox.SelectedItem = LanguageBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)(i.Tag ?? i.Content) == savedLanguage);
                ThemeBox.SelectedItem = ThemeBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Content == savedTheme);
                SpeedLimitTextBox.Text = savedSpeedLimit;
            }
            else
            {
                DisplayNameBox.Text = _user.WarframeDisplayName ?? "";
                PlatformBox.SelectedItem = _user.Platform ?? "pc";
                SpeedLimitTextBox.Text = "0";
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(string.Format((string)System.Windows.Application.Current.Resources["SaveConfirmationStr"]),
                string.Format((string)System.Windows.Application.Current.Resources["SaveSettingsStr"]), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            string displayName = DisplayNameBox.Text.Trim();
            string platform = ((ComboBoxItem)PlatformBox.SelectedItem).Content.ToString() ?? "pc";
            string lang = ((ComboBoxItem)LanguageBox.SelectedItem).Tag.ToString();
            string theme = ((ComboBoxItem)ThemeBox.SelectedItem).Content.ToString();
            string speedLimitText = SpeedLimitTextBox.Text.Trim();
            if (string.IsNullOrEmpty(speedLimitText)) speedLimitText = "0";

            if (!Regex.IsMatch(speedLimitText, @"^\d+$"))
            {
                MessageBox.Show((string)System.Windows.Application.Current.Resources["InvalidSpeedLimitStr"],
                    (string)System.Windows.Application.Current.Resources["InvalidInputStr"],
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = await AuthService.LinkWarframeAccount(_user.Id, displayName, platform);

            LanguageManager.SetLanguage(lang);
            ThemeManager.ApplyTheme(theme);

            IniFileService.Write("Account", "DisplayName", displayName);
            IniFileService.Write("Account", "Platform", platform);
            IniFileService.Write("Account", "Language", lang);
            IniFileService.Write("Account", "Theme", theme);
            IniFileService.Write("Download", "SpeedLimit", speedLimitText);
            LoggerService.Log("Settings Changed", $"{_user.Name} changed settings");
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

        private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(string.Format((string)Application.Current.Resources["DeleteAccountConfirmationStr"]),
                string.Format((string)Application.Current.Resources["DeleteAccountStr"]), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }
            try
            {
                var success = await Task.Run(() => DbService.DeleteUser(_user));

                if (success)
                {
                    LoggerService.Log("Account Deleted", $"{_user.Name} deleted their account");
                    MessageBox.Show((string)Application.Current.Resources["AccountDeletedStr"]);
                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    
                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                    mainWindow?.Close();
                    this.Close();
                }
                else
                {
                    MessageBox.Show((string)Application.Current.Resources["AccountDeletionFailedStr"]);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Log("Account Deletion Failed", $"Error deleting account for {_user.Name}: {ex.Message}");
                MessageBox.Show((string)Application.Current.Resources["AccountDeletionFailedStr"]);
            }
        }
    }
}
