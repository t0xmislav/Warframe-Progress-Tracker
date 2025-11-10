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
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class DashboardWindow : Window
    {
        private readonly Model.User _currentUser;
        public DashboardWindow(Model.User user)
        {
            InitializeComponent();
            _currentUser = user;
            Title = $"Warframe Tracker - {_currentUser.Name}";
            Loaded += DashboardWindow_Loaded;
        }
        private async void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUserProgress();
            LoadDashboard();
        }
        private Task LoadUserProgress()
        {
            var tcs = new TaskCompletionSource();
            ThreadPoolManager.QueueDatabaseRead(async () =>
            {
                ProgressCacheUtil.PreloadUserProgress(_currentUser);
                tcs.SetResult();
            });
            return tcs.Task;
        }
        private void LoadDashboard()
        {
            MainContent.Content = new DashboardView(_currentUser);
        }
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
        private void OpenDashboard_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new DashboardView(_currentUser);
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
            if (MessageBox.Show((string)Application.Current.Resources["ConfirmItemFetchStr"],
                (string)Application.Current.Resources["FetchItemsStr"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            var dialog = new LoadingDialog((string)Application.Current.Resources["FetchItemsLoadingStr"]);

            var progress = new Progress<string>(msg => dialog.UpdateMessage(msg));
            dialog.Owner = Application.Current.MainWindow;
            dialog.Show();

            var cts = new CancellationTokenSource();
            dialog.OnCancel += () => cts.Cancel();
            try
            {
                int added = 0;
                await Task.Run(async () =>
                {
                    var items = await ApiService.FetchItemsAsync(progress, cts.Token);

                    if(items.Count == 0)
                    {
                        dialog.UpdateMessage((string)Application.Current.Resources["NoItemsStr"]);
                        return;
                    }
                    dialog.UpdateMessage((string)Application.Current.Resources["SavingItemsStr"]);
                    ThreadPoolManager.QueueDatabaseWrite(() =>
                    {
                        added = DbService.SaveItems(items);
                        return Task.CompletedTask;
                    });

                }, cts.Token);
                MessageBox.Show(string.Format((string)Application.Current.Resources["ItemsAddedStr"], added), (string)Application.Current.Resources["DbUpdatedStr"]);
            }
            catch (OperationCanceledException)
            {
                dialog.SafeClose();
                MessageBox.Show((string)Application.Current.Resources["FetchItemsCancelled"], (string)Application.Current.Resources["CancelledStr"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                dialog.SafeClose();
                MessageBox.Show(ex.Message, (string)Application.Current.Resources["FetchItemsErrorStr"]);
            }
            finally
            {
                dialog.SafeClose();
            }

        }
        private async void PopulateNodes_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show((string)Application.Current.Resources["ConfirmNodeFetchStr"],
                (string)Application.Current.Resources["FetchNodesStr"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            var dialog = new LoadingDialog((string)Application.Current.Resources["LoadingScrapingNodesStr"]);
            dialog.Owner = Application.Current.MainWindow;
            var progress = new Progress<string>(msg => dialog.UpdateMessage(msg));
            dialog.Show();

            var cts = new CancellationTokenSource();
            dialog.OnCancel += () => cts.Cancel();
            int added = 0;
            try
            {
                await Task.Run(async () =>
                {
                    
                    var nodes = await WikiScraperService.ScrapeNodesAsync(progress, cts.Token);
                    dialog.UpdateMessage((string)Application.Current.Resources["LoadingSavingNodesStr"]);
                    ThreadPoolManager.QueueDatabaseWrite(() =>
                    {
                        added = DbService.SaveNodes(nodes);
                        return Task.CompletedTask;
                    });
                }, cts.Token);
                MessageBox.Show(string.Format((string)Application.Current.Resources["LoadingSavingNodesStr"], added), (string)Application.Current.Resources["DbUpdatedStr"]);
            }
            catch (OperationCanceledException)
            {
                dialog.SafeClose();
                MessageBox.Show((string)Application.Current.Resources["FetchNodesCancelledStr"], (string)Application.Current.Resources["CancelledStr"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                dialog.SafeClose();
                MessageBox.Show($"{ex.Message}", (string)Application.Current.Resources["FetchNodesErrorStr"]);
            }
            finally
            {
                dialog.SafeClose();
            }
        }
        
        
    }
}