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
        private void OpenCodex_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new CodexView(_currentUser);
        }
        private void OpenCreateEntry_Click(object sender, RoutedEventArgs e)
        {
            var createWindow = new CreateEntryWindow(_currentUser);
            createWindow.ShowDialog();
        }
        private async void PopulateDb_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show("Are you sure you want to fetch items? It might take a couple minutes.", 
                "Fetch Items", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            var dialog = new LoadingDialog("Fetching items from API, please wait...");
            dialog.Owner = Application.Current.MainWindow;
            dialog.Show();
            try
            {
                int added = 0;
                await Task.Run(async () =>
                {
                    var progress = new Progress<string>(msg => dialog.UpdateMessage(msg));
                    var items = await ApiService.FetchItemsAsync(progress);
                    dialog.UpdateMessage("Saving items to database...");
                    added = DbService.SaveItems(items);

                });
                MessageBox.Show($"{added} new items added", "Database Update");
            }catch(Exception ex)
            {
                MessageBox.Show($"Error while fetching items:\n{ex.Message}");
            }
            finally
            {
                dialog.SafeClose();
            }

        }
        private async void PopulateNodes_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to fetch nodes?", 
                "Fetch Nodes", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            var dialog = new LoadingDialog("Scraping nodes from Wiki, please wait...");
            dialog.Owner = Application.Current.MainWindow;
            dialog.Show();
            int added = 0;
            try
            {
                await Task.Run(async () =>
                {
                    var progress = new Progress<string>(msg => dialog.UpdateMessage(msg));
                    var nodes = await WikiScraperService.ScrapeNodesAsync(progress);
                    dialog.UpdateMessage("Saving nodes to database...");
                    added = DbService.SaveNodes(nodes);
                });
                MessageBox.Show($"{added} new nodes added", "Database Update");
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Error while fetching nodes:\n{ex.Message}");
            }
            finally
            {
                dialog.SafeClose();
            }
        }
        
        public MainWindow(Model.User user)
        {
            InitializeComponent();
            _currentUser = user;
            Title = $"Warframe Tracker - {_currentUser.Name}";
        }
    }
}