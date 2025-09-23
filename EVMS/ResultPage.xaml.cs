using EVMS.Service;
using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace EVMS
{
    public partial class ResultPage : UserControl
    {
        private bool useFirstDesign = true;
        private bool _showLeft = true;
        private List<PartReadingDataModel> parameterData;

        private PlcProbeService plcProbeService;
        private DataStorageService dataStorageService;
        private readonly string _connectionString;

        // Constants for static tolerance range (+/- 0.10)
        private const double StaticGreenToleranceMinus = -0.10;
        private const double StaticGreenTolerancePlus = 0.10;

        // Hardcoded PartNumber for demo
        private readonly string activePartNumber = "12134";

        public ResultPage()
        {
            InitializeComponent();
            this.Loaded += ResultPage_Loaded;
            //this.Unloaded += ResultPage_Unloaded;

            _connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;
            dataStorageService = new DataStorageService(_connectionString);
        }

        private void ResultPage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeValveDataAndUI();
            //_ = TryAutoConnectAsync();
        }

        //private async Task TryAutoConnectAsync()
        //{
        //    if (plcProbeService == null)
        //        plcProbeService = new PlcProbeService();

        //    if (!plcProbeService.IsConnected)
        //    {
        //        bool connected = await plcProbeService.ConnectAsync();
        //        if (!connected)
        //        {
        //            MessageBox.Show("Failed to connect to PLC and probes on page load.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        //        }
        //    }
        //}

        private void InitializeValveDataAndUI()
        {
            try
            {
                // Fetch parameters from DB
                parameterData = dataStorageService.GetParametersByPartNumber(activePartNumber);
                if (parameterData == null || parameterData.Count == 0)
                {
                    MessageBox.Show($"No parameters found for part {activePartNumber}");
                    return;
                }

                ValveReadingsGrid.Columns.Clear();

                foreach (var param in parameterData)
                {
                    var column = new DataGridTextColumn
                    {
                        Header = param.Parameter
                    };
                    ValveReadingsGrid.Columns.Add(column);
                }

                ValveReadingsGrid.ItemsSource = parameterData;

                LoadProgressBars();

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

                double min = param.Nominal - param.RTolMinus;
                double max = param.Nominal + param.RTolPlus;
                double mean = param.Nominal;
                double greenLowThreshold = mean + StaticGreenToleranceMinus;
                double greenHighThreshold = mean + StaticGreenTolerancePlus;

                if (useFirstDesign)
                {
                    var pb = new ResultProgressBar { Margin = new Thickness(5) };
                    pb.ParameterName = param.Parameter;
                    pb.MinValue = min;
                    pb.MaxValue = max;
                    pb.MeanValue = mean;
                    pb.GreenLowThreshold = greenLowThreshold;
                    pb.GreenHighThreshold = greenHighThreshold;
                    progressBar = pb;
                }
                else
                {
                    var pb = new ProgresBarControl { Margin = new Thickness(5) };
                    pb.Min = min;
                    pb.Mean = mean;
                    pb.Max = max;
                    pb.Title = param.Parameter;
                    progressBar = pb;
                }

                ProgressBarContainer.Children.Add(progressBar);
            }
        }
        private void ToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_showLeft)
            {
                LeftColumn.Width = new GridLength(0);
                RightColumn.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                LeftColumn.Width = new GridLength(1, GridUnitType.Star);
                RightColumn.Width = new GridLength(0);
            }
            _showLeft = !_showLeft;
        }


        private async void MasterToggle_Checked(object sender, RoutedEventArgs e)
        {
            await MasterInitializeAsync();
        }

        private void MasterToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            
        }

        private async Task MasterInitializeAsync()
        {
           
        }


        private void PlcProbeService_ProbeValueUpdated(object? sender, ProbeReadingEventArgs e)
        {
           
        }

        //private void SetTextBoxValue(string probeName, double? value)
        //{
        //}

        private void SwitchProgressBar_Click(object sender, RoutedEventArgs e)
        {
            useFirstDesign = !useFirstDesign;
            LoadProgressBars();
            SwitchProgressBarBtn.Content = useFirstDesign ? "Switch to Design 2" : "Switch to Design 1";
        }

        //private void ResultPage_Unloaded(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        if (plcProbeService != null)
        //        {
        //            plcProbeService.StopLiveReading();
        //            plcProbeService.ProbeValueUpdated -= PlcProbeService_ProbeValueUpdated;
        //            plcProbeService.Dispose();
        //            plcProbeService = null;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log or ignore exceptions during unload
        //    }
        //}

    }
}
