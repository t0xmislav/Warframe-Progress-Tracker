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

namespace Warframe_Progress_Tracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Model.User _currentUser;
        private async void LoadData()
        {
            DbService.InitializeDatabase();

            var masteryItems = await ApiService.FetchItemsAsync();

            foreach (var item in masteryItems)
            {
                DbService.AddItem(item);
                
            }

            MessageBox.Show($"Added {masteryItems.Count} mastery items!");
        }
        public MainWindow(Model.User user)
        {
            InitializeComponent();
            _currentUser = user;
            LoadData();
            Title = $"Warframe Tracker - {_currentUser.Name}";
        }
    }
}