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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Warframe_Progress_Tracker.ViewModel;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : UserControl
    {
        private readonly DashboardViewModel _vm;
        private Model.User _currentUser;
        public DashboardView(Model.User user)
        {
            InitializeComponent();
            _currentUser = user;
            _vm = new DashboardViewModel(_currentUser);
            DataContext = _vm;
            
        }
        public void Stop()
        {
            try
            {
                _vm?.Stop();
            }
            catch { }
        }
    }
}
