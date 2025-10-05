using System.Text;
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

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Model.User _currentUser;
        
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void OpenAccountSettings_Click(object sender, RoutedEventArgs e)
        {
            var accountWindow = new AccountSettingsWindow(_currentUser);
            accountWindow.Show();
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DbService.IsItemsTableEmpty())
            {
                var items = await ApiService.FetchItemsAsync();
                int newItems = 0;

                foreach (var item in items)
                {
                    if (DbService.AddItem(item)) newItems++;
                }
                MessageBox.Show($"{newItems} items added to database");
            }
        }
        private void OpenCodex_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new CodexView(_currentUser);
        }
        private async void PopulateDb_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Adding...");
            int added = await DbService.PopulateItemsFromApi();
            MessageBox.Show($"{added} new items added", "Database Update");

        }
        private async void PopulateNodes_Click(object sender, RoutedEventArgs e)
        {
            await WikiScraperService.ScrapeNodesAsync();
        }
        public MainWindow(Model.User user)
        {
            InitializeComponent();
            _currentUser = user;
            Title = $"Warframe Tracker - {_currentUser.Name}";
        }
    }
}