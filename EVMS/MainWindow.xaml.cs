using EVMS.Service;
using System.ComponentModel;
using System.Diagnostics;  // ✅ Needed for Process.Start
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;   // 👈 this is required
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace EVMS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        // Field to keep track of the current selected MenuItem
        private DateTime lastExportDate = DateTime.MinValue;

        private readonly DataStorageService _dataService;
        private readonly MasterService masterService;
        private MenuItem? _currentlySelectedMenuItem;
        private bool isSettingsAuthenticated = false; // global flag
        private bool _isResultPageOpen = false;

        public MainWindow()
        {
            InitializeComponent();
            

            DataContext = this;
            _dataService = new DataStorageService();
            Loaded += MainWindow_Loaded;
           // Loaded += Window_Loaded;
            masterService = new MasterService();

            masterService.StatusMessageUpdated += message =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusMessageTextBox.Text = message;
                });
            };
           // GenerateTestExcelReport();
        }
        //private void Window_Loaded(object sender, RoutedEventArgs e)
        //{
        //    // Set window to cover the entire screen, including taskbar
        //    this.WindowState = WindowState.Normal;  // start in normal first
        //    this.Topmost = true;                    // always on top
        //    this.WindowStyle = WindowStyle.None;    // no border/title
        //    this.ResizeMode = ResizeMode.NoResize;

        //    this.Left = 0;
        //    this.Top = 0;
        //    this.Width = SystemParameters.PrimaryScreenWidth;
        //    this.Height = SystemParameters.PrimaryScreenHeight;
        //}
       //private void CloseButton_Click(object sender, RoutedEventArgs e)
       // {
       //     this.Close();
       // }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                var activeParts = _dataService.GetActiveParts();
                bool hasActive = activeParts.Count > 0;
                string? activeName = hasActive ? activeParts[0].Para_No : "No Active Part";

                Dispatcher.Invoke(() =>
                {
                    ActivePartName = activeName;
                    IsActivePart = hasActive;
                    ActivePartStatusButton.Content = activeName;
                });
                // Run report generation on a background thread
                try
                    {
                    // Do not use Dispatcher here – keeps this background
                    GenerateYesterdayActivePartDailyReport();                    }
                    catch (Exception ex)
                    {
                        // Only use Dispatcher for showing the error
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Failed to generate Excel report for part: {ex.Message}");
                        });
                    }
              
            });
        }




        private string _activePartName = "No Active Part";
        public string ActivePartName
        {
            get => _activePartName;
            set
            {
                _activePartName = value;
                OnPropertyChanged(nameof(ActivePartName));
            }
        }

        private bool _isActivePart;
        public bool IsActivePart
        {
            get => _isActivePart;
            set
            {
                _isActivePart = value;
                OnPropertyChanged(nameof(IsActivePart));

                // Update button background here (assuming button named ActivePartStatusButton)
                if (ActivePartStatusButton != null)
                {
                    ActivePartStatusButton.Background = _isActivePart
                        ? new SolidColorBrush(Color.FromRgb(34, 197, 94))  // Green
                        : new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));



        private void EntryPage_StartClicked(object sender, StartClickedEventArgs e)
        {
            MainContentGrid.Children.Clear();

            ResultPage resultPage = new ResultPage(e.Model, e.LotNo, e.UserId)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            resultPage.StatusMessageChanged += (message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusMessageTextBox.Text = message;
                });
            };

            // ✅ Track when ResultPage is open or closed
            resultPage.Loaded += (s, args) =>
            {
                _isResultPageOpen = true;
                EnableMenus(false);
            };

            resultPage.Unloaded += (s, args) =>
            {
                _isResultPageOpen = false;
                EnableMenus(true);
            };

            MainContentGrid.Children.Add(resultPage);
        }

        



        private void EnableMenus(bool isEnabled)
        {
            RunPartMenu.IsEnabled = isEnabled;
            MastringConfigMenu.IsEnabled = isEnabled;
            ReportMenu.IsEnabled = isEnabled;
            SettingsMenu.IsEnabled = isEnabled;
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

            // Subscribe to status message changes
            resultPage.StatusMessageChanged += (message) =>
            {
                Dispatcher.Invoke(() =>  // Ensure UI thread update
                {
                    StatusMessageTextBox.Text = message;
                });
            };

            resultPage.HorizontalAlignment = HorizontalAlignment.Stretch;
            resultPage.VerticalAlignment = VerticalAlignment.Stretch;

            MainContentGrid.Children.Add(resultPage);
        }


        private void Report_Page(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            Report_GraphPage resultPage = new Report_GraphPage();

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

        private void CO_Click(object sender, RoutedEventArgs e)
        {
            MainContentGrid.Children.Clear();
            SettingsPage resultPage = new SettingsPage();

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
            // Subscribe to status message changes
            resultPage.StatusMessageChanged += (message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusMessageTextBox.Text = message;
                });
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
            // 🔒 1️⃣ Check if ResultPage is open
            if (_isResultPageOpen)
            {
                e.Handled = true; // prevent menu opening
                MessageBox.Show("⚠ Please close the Operation before accessing the menu.",
                                "Action Not Allowed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            // 🔐 2️⃣ Then check if user is authenticated
            if (!isSettingsAuthenticated)
            {
                e.Handled = true; // stop default behavior until login succeeds

                Login_Page login = new Login_Page();
                login.Owner = this;
                bool? result = login.ShowDialog();

                if (result == true && login.IsAuthenticated)
                {
                    isSettingsAuthenticated = true; // unlock for this session

                    // ✅ Open Settings menu manually after login success
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
                    MessageBox.Show("❌ Access Denied! Wrong Username or Password.",
                                    "Restricted",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
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


        private void GenerateYesterdayActivePartDailyReport()
        {
            Task.Run(() =>
            {
                string baseExportFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "EVMS_Exports"
                );

                string companyName = "MEPL";
                DateTime yesterday = DateTime.Today.AddDays(-1);

                try
                {
                    if (lastExportDate == yesterday)
                        return;

                    var activeParts = _dataService.GetActiveParts();
                    if (activeParts == null || activeParts.Count == 0)
                        return;

                    string activePartNo = activeParts[0].Para_No;
                    string folderPath = Path.Combine(baseExportFolder, companyName, activePartNo);
                    Directory.CreateDirectory(folderPath);

                    var dataExportService = new DataExportService(_dataService, folderPath);
                    dataExportService.ExportDailyCumulativeReport(yesterday, activePartNo);

                    lastExportDate = yesterday;
                }
                catch (Exception ex)
                {
                    string logFile = Path.Combine(baseExportFolder, "ExportError.log");
                    File.AppendAllText(logFile, $"{DateTime.Now}: {ex}\n");

                    if (Application.Current?.Dispatcher != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"Failed to generate Excel report: {ex.Message}")
                        );
                    }
                }
            });
        }








    }
}
