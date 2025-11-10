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

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            DbService.InitializeDatabase();
        }
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var user = AuthService.Login(UsernameBox.Text, PasswordBox.Password);
            if (user != null)
            {
                MessageBox.Show(string.Format((string)Application.Current.Resources["WelcomeStr"], user.Name));
                var main = new DashboardWindow(user);
                main.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show((string)Application.Current.Resources["InvalidLoginStr"]);
            }
        }
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = new RegisterWindow();
            registerWindow.ShowDialog();
        }
    }
}
