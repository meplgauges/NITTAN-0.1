using System.Diagnostics;  // ✅ Needed for Process.Start
using System.Windows;
using System.Windows.Controls;   // 👈 this is required
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace EVMS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();


        }
        private void RunMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Clear existing content
            MainContentGrid.Children.Clear();

            // Create instance of RunPage UserControl
            ProbeSetup runPage = new ProbeSetup();

            // Make sure it fills the MainContentGrid
            runPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            runPage.VerticalAlignment = VerticalAlignment.Stretch;

            // Add RunPage to the container Grid
            MainContentGrid.Children.Add(runPage);
        }

        private void Mesurment_Click(object sender, RoutedEventArgs e)
        {
            // Clear existing content
            MainContentGrid.Children.Clear();

            // Create instance of RunPage UserControl
            ResultPage resultPage = new ResultPage();

            // Make sure it fills the MainContentGrid
            resultPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            resultPage.VerticalAlignment = VerticalAlignment.Stretch;

            // Add RunPage to the container Grid
            MainContentGrid.Children.Add(resultPage);
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
