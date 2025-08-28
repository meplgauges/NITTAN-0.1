using System.Windows;

namespace EVMS
{
    public partial class Login_Page : Window
    {
        public bool IsAuthenticated { get; private set; } = false;

        public Login_Page()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = txtPassword.Password;

            // ✅ Authentication check
            if (username == "admin" && password == "1234")
            {
                IsAuthenticated = true;
                this.DialogResult = true; // Success
                this.Close();
            }
            else
            {
                IsAuthenticated = false;
                this.DialogResult = false; // ❌ must set false!
                MessageBox.Show("Invalid Username or Password!", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsAuthenticated = false;
            this.DialogResult = false;
            this.Close();
        }
    }
}
