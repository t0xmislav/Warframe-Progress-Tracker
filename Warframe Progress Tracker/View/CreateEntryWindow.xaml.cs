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
using Warframe_Progress_Tracker.ViewModel;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for CreateEntryWindow.xaml
    /// </summary>
    public partial class CreateEntryWindow : Window
    {
        private CreateEntryViewModel _vm;
        private Model.User _currentUser;
        public CreateEntryWindow(Model.User user)
        {
            InitializeComponent();
            _currentUser = user;
            _vm = new CreateEntryViewModel(_currentUser);
            DataContext = _vm;
            _vm.RequestClose += (result) =>
            {
                if (IsLoaded && (result is bool b))
                    DialogResult = b;
                Close();
            };
        }
    }
}
