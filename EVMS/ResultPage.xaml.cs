using EVMS.Service;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace EVMS
{
    public partial class ResultPage : UserControl
    {
        private bool useFirstDesign = true;
        private bool _showLeft = true;
        private List<PartReadingDataModel> parameterData;
        private string activePartNumber = string.Empty;

        private PlcProbeService plcProbeService;
        private DataStorageService dataStorageService;
        private readonly string _connectionString;

        // Constants for static tolerance range (+/- 0.10)
        private const double StaticGreenToleranceMinus = -0.10;
        private const double StaticGreenTolerancePlus = 0.10;

        public ResultPage()
        {
            InitializeComponent();
            this.Loaded += ResultPage_Loaded;

            _connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;
            dataStorageService = new DataStorageService(_connectionString);
        }

        private void ResultPage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeValveDataAndUI();
        }

        private void InitializeValveDataAndUI()
        {
            try
            {
                // Get active parts
                var activeParts = dataStorageService.GetActiveParts();
                if (activeParts == null || activeParts.Count == 0)
                {
                    MessageBox.Show("No active parts found.");
                    return;
                }

                activePartNumber = activeParts[0].Para_No ?? string.Empty;

                // Get parameters for active part
                parameterData = dataStorageService.GetPartConfigByPartNumber(activePartNumber);
                if (parameterData == null || parameterData.Count == 0)
                {
                    MessageBox.Show($"No parameters found for active part {activePartNumber}");
                    return;
                }

                // Setup DataGrid dynamically
                LoadDataGrid();

                // Load progress bars
                LoadProgressBars();

                // Set toggle button text
                SwitchProgressBarBtn.Content = useFirstDesign ? "Switch to Design 2" : "Switch to Design 1";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}");
            }
        }

        private void LoadDataGrid()
        {
            ValveReadingsGrid.Columns.Clear();

            // Transform parameterData into a DataTable for DataGrid binding
            DataTable dt = new DataTable();

            // Add columns for each parameter
            foreach (var param in parameterData)
            {
                dt.Columns.Add(param.Parameter, typeof(double));
            }

            // Add a single row of current values
            //DataRow row = dt.NewRow();
            //foreach (var param in parameterData)
            //{
            //    row[param.Parameter] = param.Value;
            //}
            //dt.Rows.Add(row);

            ValveReadingsGrid.ItemsSource = dt.DefaultView;
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

        private void SwitchProgressBar_Click(object sender, RoutedEventArgs e)
        {
            useFirstDesign = !useFirstDesign;
            LoadProgressBars();
            SwitchProgressBarBtn.Content = useFirstDesign ? "Switch to Design 2" : "Switch to Design 1";
        }

        private async void MasterToggle_Checked(object sender, RoutedEventArgs e)
        {
            // Call your initialization logic
            //await MasterInitializeAsync();
        }

        private void MasterToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // Optional: handle toggle off event
        }

    }
}
