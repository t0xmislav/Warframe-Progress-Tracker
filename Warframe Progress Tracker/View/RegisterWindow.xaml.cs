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
    /// Interaction logic for RegisterWindow.xaml
    /// </summary>
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show((string)Application.Current.Resources["MissingRegistrationStr"]);
                return;
            }
            if (password != confirmPassword)
            {
                MessageBox.Show((string)Application.Current.Resources["MismatchedPasswordsStr"]);
                return;
            }
            if (AuthService.Register(username, password))
            {
                MessageBox.Show((string)Application.Current.Resources["RegistrationSuccessStr"]);
                this.Close();
            }
            else
            {
                MessageBox.Show((string)Application.Current.Resources["DuplicateUsernameStr"]);
            }
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
