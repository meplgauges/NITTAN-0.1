using System.Diagnostics;  // ✅ Needed for Process.Start
using System.Windows;
using System.Windows.Controls;   // 👈 this is required
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace EVMS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        // Field to keep track of the current selected MenuItem
        private MenuItem? _currentlySelectedMenuItem;
        private bool isSettingsAuthenticated = false; // global flag
        public MainWindow()
        {
            InitializeComponent();
            
        }

        private void EntryPage_StartClicked(object sender, StartClickedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            ResultPage resultPage = new ResultPage
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Pass entered data to ResultPage
            //resultPage.SetData(e.Model, e.LotNo, e.UserId);

            MainContentGrid.Children.Add(resultPage);
        }

        private void HomePage_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            HomePage runPage = new HomePage();
            runPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            runPage.VerticalAlignment = VerticalAlignment.Stretch;
            MainContentGrid.Children.Add(runPage);
        }
        private void RunMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            ProbeSetupPage runPage = new ProbeSetupPage();
            runPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            runPage.VerticalAlignment = VerticalAlignment.Stretch;
            MainContentGrid.Children.Add(runPage);
        }

        private void ProgressBar_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            ResultProgressBar runPage = new ResultProgressBar();
            runPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            runPage.VerticalAlignment = VerticalAlignment.Stretch;
            MainContentGrid.Children.Add(runPage);
        }

        private void Mesurment_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            EntryPage entryPage = new EntryPage();

            entryPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            entryPage.VerticalAlignment = VerticalAlignment.Stretch;

            entryPage.StartClicked += EntryPage_StartClicked;
            MainContentGrid.Children.Add(entryPage);
        }

        private void MasterPage_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            MasterReadingPage resultPage = new MasterReadingPage();

            resultPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            resultPage.VerticalAlignment = VerticalAlignment.Stretch;

            MainContentGrid.Children.Add(resultPage);
        }

        private void LoginSetup_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            AdminControlePage resultPage = new AdminControlePage();

            resultPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            resultPage.VerticalAlignment = VerticalAlignment.Stretch;

            MainContentGrid.Children.Add(resultPage);
        }

        private void RoundBar_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            ResultProgressBar resultPage = new ResultProgressBar();

            resultPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            resultPage.VerticalAlignment = VerticalAlignment.Stretch;

            MainContentGrid.Children.Add(resultPage);
        }

        private void IO_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            IO_Controle_page resultPage = new IO_Controle_page();

            resultPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            resultPage.VerticalAlignment = VerticalAlignment.Stretch;

            MainContentGrid.Children.Add(resultPage);
        }
        
        private async void ProbeInstall_Click(object sender, RoutedEventArgs e)
        {
            // Show message box
            MessageBox.Show("Please wait, initializing...", "Loading", MessageBoxButton.OK, MessageBoxImage.Information);

            // Simulate delay (e.g., 2 seconds)
            await Task.Delay(2000);

            // Then load your page
            MainContentGrid.Children.Clear();
            var resultPage = new ProbeInstallPage()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            MainContentGrid.Children.Add(resultPage);
        }



        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem clickedMenuItem)
                return;

            // Reset foreground color of previously selected MenuItem
            if (_currentlySelectedMenuItem != null)
            {
                _currentlySelectedMenuItem.Foreground = (Brush)FindResource("MenuItemForegroundBrush") ?? Brushes.White;
            }

            // Set foreground color of currently clicked MenuItem to red
            clickedMenuItem.Foreground = Brushes.Red;

            // Update reference
            _currentlySelectedMenuItem = clickedMenuItem;
        }


        private void PartConfig_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            PartConfig resultPage = new PartConfig();
            resultPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            resultPage.VerticalAlignment = VerticalAlignment.Stretch;
            MainContentGrid.Children.Add(resultPage);
        }

        private void PartManager_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            Part_Manager resultPage = new Part_Manager();
            resultPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            resultPage.VerticalAlignment = VerticalAlignment.Stretch;
            MainContentGrid.Children.Add(resultPage);
        }

        
        private void SettingsMenu_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isSettingsAuthenticated)
            {
                e.Handled = true; // stop default behavior until login succeeds

                Login_Page login = new Login_Page();
                login.Owner = this;
                bool? result = login.ShowDialog();

                if (result == true && login.IsAuthenticated)
                {
                    isSettingsAuthenticated = true; // unlock for this session
                   // MessageBox.Show("✅ Settings unlocked!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Open Settings menu manually
                    if (sender is MenuItem menu)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            menu.IsSubmenuOpen = true;
                        }));
                    }
                }
                else
                {
                    MessageBox.Show("❌ Access Denied! Wrong Username or Password.", "Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }



        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OpenDashboard_Click(object sender, RoutedEventArgs e)
        {
            SplashScreenPage dash = new SplashScreenPage();
            dash.Show();
        }

        private void OpenReports_Click(object sender, RoutedEventArgs e)
        {
            SplashScreenPage reports = new SplashScreenPage();
            reports.Show();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            SplashScreenPage settings = new SplashScreenPage();
            settings.Show();
        }

        // ✅ NEW: Handle Email link click
        private void EmailLink_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "mailto:info@meplgauges.com",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open the email client: " + ex.Message);
            }
        }

        // ✅ NEW: Handle website link click
        private void WebsiteLink_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Opens website in default browser
                Process.Start(new ProcessStartInfo("https://meplgauges.com/")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Unable to open website.");
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActualWidth < 600)
            {
                // Switch to single column
                MainContentGrid.ColumnDefinitions.Clear();
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(CompanyInfoPanel, 0);
                Grid.SetColumn(LogoPanel, 0);

                LogoPanel.Margin = new Thickness(0, 20, 0, 0); // push logo down
            }
            else
            {
                // Switch back to two columns
                MainContentGrid.ColumnDefinitions.Clear();
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(2, GridUnitType.Star) });

                Grid.SetColumn(CompanyInfoPanel, 0);
                Grid.SetColumn(LogoPanel, 1);

                LogoPanel.Margin = new Thickness(0);
            }
        }


    }
}
