using EVMS.Service;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EVMS
{
    public partial class ResultPage : UserControl
    {
        private bool useFirstDesign = true;
        private bool _showLeft = true;

        private List<PartReadingDataModel> parameterData;
       // private List<ProbeInstallModel> probeData;
        private PlcProbeService plcProbeService;
        private DataStorageService dataStorageService;
        private readonly string _connectionString;
        // Constants for static tolerance range (+/- 0.10)
        private const double StaticGreenToleranceMinus = -0.10;
        private const double StaticGreenTolerancePlus = 0.10;


        // ✅ Hardcoded PartNumber
        private readonly string activePartNumber = "12134"; 

        public ResultPage()
        {
            InitializeComponent();
            this.Loaded += ResultPage_Loaded;
            this.Unloaded += ResultPage_Unloaded;

            _connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;
            plcProbeService = new PlcProbeService();
            dataStorageService = new DataStorageService(_connectionString);

            this.Loaded += RunPage_Loaded;
        }

        private async void RunPage_Loaded(object sender, RoutedEventArgs e)
        {
            bool connected = await plcProbeService.ConnectAsync();

            if (connected)
            {
                MessageBox.Show("PLC and Probes Connected ✅");
            }
            else
            {
                MessageBox.Show(
                    "Failed to connect to PLC and probes. Please check connections.",
                    "Connection Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ResultPage_Unloaded(object sender, RoutedEventArgs e)
        {
            plcProbeService.Disconnect();
            MessageBox.Show("PLC and Probes Disconnected ❌");
        }

        private void ResultPage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeValveDataAndUI();
        }

        private void InitializeValveDataAndUI()
        {
            try
            {
                // Fetch parameters
                parameterData = dataStorageService.GetParametersByPartNumber(activePartNumber);

                if (parameterData == null || parameterData.Count == 0)
                {
                    MessageBox.Show($"No parameters found for part {activePartNumber}");
                    return;
                }

                // Clear existing columns
                ValveReadingsGrid.Columns.Clear();

                // Dynamically add a column for each property you want to display
                foreach (var param in parameterData)
                {
                    var column = new DataGridTextColumn
                    {
                        Header = param.Parameter,                     // Column name = Parameter
                        //Binding = new System.Windows.Data.Binding("Value") // Bind to Value property of the model
                    };
                    ValveReadingsGrid.Columns.Add(column);
                }

                // Bind the parameter data to the DataGrid
                ValveReadingsGrid.ItemsSource = parameterData;

                // Load progress bars
                LoadProgressBars();

                // Update button text
                SwitchProgressBarBtn.Content = useFirstDesign ? "Switch to Design 2" : "Switch to Design 1";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}");
            }
        }



        private void LoadProgressBars()
        {
            ProgressBarContainer.Children.Clear();

            foreach (var param in parameterData)
            {
                UserControl progressBar;

                // Calculate limits
                double min = param.Nominal - param.RTolMinus;
                double max = param.Nominal + param.RTolPlus;
                double mean = param.Nominal;
                double greenLowThreshold = mean + StaticGreenToleranceMinus;  // Nominal - 0.10
                double greenHighThreshold = mean + StaticGreenTolerancePlus;  // Nominal + 0.10
                // Calculate normalized progress value
                //double normalizedValue = 0;
                //if (param.CurrentReadingAvailable)
                //{
                //    double reading = param.CurrentReading;
                //    if (reading < min)
                //        normalizedValue = 0;
                //    else if (reading > max)
                //        normalizedValue = 100;
                //    else
                //        normalizedValue = ((reading - min) / (max - min)) * 100;
                //}

                if (useFirstDesign)
                {
                    var pb = new ResultProgressBar { Margin = new Thickness(5) };

                    pb.ParameterName = param.Parameter;

                    //// Assume ResultProgressBar has similar properties or methods to set limits and value
                    pb.MinValue = min;        // Set minimum limit
                    pb.MaxValue = max;        // Set maximum limit
                    pb.MeanValue = mean;      // Set current/mean value for the needle
                    pb.GreenLowThreshold = greenLowThreshold;
                    pb.GreenHighThreshold = greenHighThreshold;
                    //pb.Value = normalizedValue;

                    progressBar = pb;
                }
                else
                {
                    var pb = new ProgresBarControl { Margin = new Thickness(5) };
                    pb.Min = min;
                    pb.Mean = mean;
                    pb.Max = max;
                    pb.Title = param.Parameter;
                   // pb.Value = normalizedValue;

                    progressBar = pb;
                }

                ProgressBarContainer.Children.Add(progressBar);
            }
        }

        private void ProgressBarContainer_SizeChanged(object sender, SizeChangedEventArgs e)
{
    double availableWidth = e.NewSize.Width;
    double childWidth = availableWidth / 6; // for 6 columns
    foreach (UIElement child in ProgressBarContainer.Children)
    {
        if (child is FrameworkElement fe)
        {
            fe.Width = childWidth;  // dynamically set child width
            // Optionally set MinWidth, MinHeight here too
            fe.MinWidth = 100; // example minimum width
            fe.Height = childWidth; // or any height ratio you want
        }
    }
}
        private void ToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_showLeft)
            {
                // Show only RIGHT column
                LeftColumn.Width = new GridLength(0);
                RightColumn.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // Show only LEFT column
                LeftColumn.Width = new GridLength(1, GridUnitType.Star);
                RightColumn.Width = new GridLength(0);
            }

            _showLeft = !_showLeft;
        }

        private void SwitchProgressBar_Click(object sender, RoutedEventArgs e)
        {
            useFirstDesign = !useFirstDesign;
            LoadProgressBars();

            SwitchProgressBarBtn.Content = useFirstDesign ? "Switch to Design 2" : "Switch to Design 1";
        }
    }
}
