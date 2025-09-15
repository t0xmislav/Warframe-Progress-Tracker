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

namespace Warframe_Progress_Tracker
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
        }

        private void Save_Click(Object sender, RoutedEventArgs e)
        {
            string displayName = DisplayNameBox.Text.Trim();
            string platform = ((ComboBoxItem)PlatformBox.SelectedItem).Content.ToString();

            AuthService.LinkWarframeAccount(_user.Id, displayName, platform);

            MessageBox.Show("Account settings updated");
            this.Close();
        }

        private void Cancel_Click(Object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
