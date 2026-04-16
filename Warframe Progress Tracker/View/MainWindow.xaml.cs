using Microsoft.Win32;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
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
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Model.User _currentUser;
        public MainWindow(Model.User user)
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
        private void OpenLogView_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new LogView(_currentUser);
        }
        private async void PopulateDb_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show((string)Application.Current.Resources["ConfirmItemFetchStr"],
                (string)Application.Current.Resources["FetchItemsStr"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            LoggerService.Log("Item Fetching Started", $"{_currentUser.Name}: Initiated fetching items from api.");
            var dialog = new LoadingDialog("FetchItemsLoadingStr");

            var progress = new Progress<(string key, object[] args)>(msg => dialog.UpdateMessage(msg.key, msg.args));
            dialog.Owner = Application.Current.MainWindow;
            dialog.Show();

            var cts = new CancellationTokenSource();
            dialog.OnCancel += () => cts.Cancel();
            try
            {
                var uniqueNames = DbService.GetAllUniqueItemNames();
                int added = 0;
                await ApiService.FetchItemsAsync(progress, cts.Token, uniqueNames);
                /*await Task.Run(async () =>
                {
                    var items = await ApiService.FetchItemsAsync(progress, cts.Token, uniqueNames);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        dialog.UpdateMessage("SavingItemsStr", new object[] { });
                    });

                    added = DbService.SaveItems(items);

                }, cts.Token);*/
                LoggerService.Log("Item Fetching Finished", $"{_currentUser.Name}: Finished fetching node from api | Added: {added} items.");
                MessageBox.Show((string)Application.Current.Resources["ItemsAddedStr"], (string)Application.Current.Resources["DbUpdatedStr"]);
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
            LoggerService.Log("Node Scraping Started", $"{_currentUser.Name}: Initiated scraping nodes from wiki.");
            var dialog = new LoadingDialog("LoadingScrapingNodesStr");
            dialog.Owner = Application.Current.MainWindow;
            var progress = new Progress<(string key, object[] args)>(msg => dialog.UpdateMessage(msg.key, msg.args));
            dialog.Show();

            var cts = new CancellationTokenSource();
            dialog.OnCancel += () => cts.Cancel();
            int added = 0;
            try
            {

                await Task.Run(async () =>
                {
                    
                    var nodes = await WikiScraperService.ScrapeNodesAsync(progress, cts.Token);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        dialog.UpdateMessage("LoadingSavingNodesStr", new object[] { });
                    });
                    added = DbService.SaveNodes(nodes);
                    
                }, cts.Token);
                LoggerService.Log("Nodes Scraped", $"{_currentUser.Name}: finished scraping nodes | Added: {added} nodes");
                MessageBox.Show(string.Format((string)Application.Current.Resources["NodesAddedStr"], added), (string)Application.Current.Resources["DbUpdatedStr"]);
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
        private string SelectFolder()
        {
            var dialog = new OpenFolderDialog();
            dialog.ShowDialog();
            var folderName = dialog.FolderName;

            return folderName;
        }

        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            var outputPath = SelectFolder();
            if(string.IsNullOrEmpty(outputPath))
            {
                return;
            }
            var xmlPath = ReportGeneratorUtil.SaveReportXml(_currentUser);

            var (result, message) = await ReportGeneratorUtil.GenerateReportAsync(xmlPath, outputPath);

            if (result)
            {
                MessageBox.Show((string)Application.Current.Resources["ReportGeneratedStr"]);
            }
            else
            {
                MessageBox.Show(string.Format((string)Application.Current.Resources["ReportGenerationFailedStr"] + message), 
                    (string)Application.Current.Resources["ErrorStr"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
        }
        private async void ExportProgress_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var xml = XmlUtil.GenerateUserProgressXml(_currentUser);
                var (success, pdfPath) = await RsaUtil.ExportProgressWithSignature(xml);
                if (success)
                    MessageBox.Show($"Progress exported successfully:\n{pdfPath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportProgress_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog { Filter = "XML Progress|*.xml" };
                if (dlg.ShowDialog() != true) return;

                var (verified, xml) = RsaUtil.VerifyProgressFile(dlg.FileName);
                if (!verified)
                {
                    MessageBox.Show("Signature verification failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                Console.WriteLine("Verified XML:\n" + xml);
                var success = RsaUtil.ApplyProgressSnapshot(xml);
                if (!success)
                {
                    ProgressCacheUtil.PreloadUserProgress(_currentUser); // Refresh cache to ensure consistency
                    MessageBox.Show("Failed to apply progress snapshot.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                // Parse and apply XML to database
                MessageBox.Show("Progress imported successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        

    }
}