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
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;
            string CurrentUserRole = string.Empty;

            // Example: simple hardcoded users for demo
            // In production, validate using database or secure source
            if ((username == "" && password == ""))
            {
                IsAuthenticated = true;
                CurrentUserRole = "admin"; // Store the role as needed
                this.DialogResult = true;
                this.Close();
            }
            else if ((username == "" && password == ""))
            {
                IsAuthenticated = true;
                CurrentUserRole = "operator"; // Store the role as needed
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                IsAuthenticated = false;
                this.DialogResult = false;
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
