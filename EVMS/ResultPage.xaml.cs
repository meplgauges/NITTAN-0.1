using EVMS.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
        private MasterService _masterService;


        private const double StaticGreenToleranceMinus = -0.10;
        private const double StaticGreenTolerancePlus = 0.10;

        public ResultPage()
        {
            InitializeComponent();
            this.Loaded += ResultPage_Loaded;
            this.Unloaded += ResultPage_Unloaded;


            dataStorageService = new DataStorageService();
            _masterService = new MasterService();
            plcProbeService= new PlcProbeService();
            // Add this line
        }

        private async void ResultPage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeValveDataAndUI();
            try
            {
                // Ensure PLC and Probe Connection asynchronously when page loads
                bool connected = await _masterService.EnsureConnectionAsync();
                if (connected)
                {
                    MessageBox.Show("PLC and Probe Connected Successfully", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to connect to PLC and Probe", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during connection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResultPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _masterService.Cleanup();
        }


        private void InitializeValveDataAndUI()
        {
            try
            {
                var activeParts = dataStorageService.GetActiveParts();
                if (activeParts == null || activeParts.Count == 0)
                {
                    MessageBox.Show("No active parts found.");
                    return;
                }

                activePartNumber = activeParts[0].Para_No ?? string.Empty;
                parameterData = dataStorageService.GetPartConfigByPartNumber(activePartNumber);

                if (parameterData == null || parameterData.Count == 0)
                {
                    MessageBox.Show($"No parameters found for active part {activePartNumber}");
                    return;
                }

                LoadDataGrid();
                LoadProgressBars();
                SwitchProgressBarBtn.Content = useFirstDesign ? "Switch to Design 2" : "Switch to Design 1";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}");
            }
        }

        private void ValveReadingsGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Set all generated columns to star sizing so they share available width evenly
            e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);

            // Center align the header text
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            e.Column.HeaderStyle = headerStyle;
        }

        private void LoadDataGrid()
{
    ValveReadingsGrid.Columns.Clear();

    DataTable dt = new();

    foreach (var param in parameterData)
    {
        dt.Columns.Add(param.Parameter, typeof(double));
    }

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
            if (sender is ToggleButton toggleButton)
                toggleButton.IsEnabled = false;

            try
            {
                if (sender is ToggleButton tb)
                {
                    await _masterService.MasterCheckProcedureAsync();
                    MessageBox.Show("Mastering started and completed.");
                    tb.IsChecked = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting mastering: {ex.Message}");
            }
            finally
            {
                if (sender is ToggleButton tb)
                    tb.IsEnabled = true;
            }
        }




        //private void MasterToggleButton_Unchecked(object sender, RoutedEventArgs e)
        //{
        //    // Called when toggle is switched OFF
        //    try
        //    {
        //        // Stop mastering or cleanup resources here
        //        // For example, stop live reading or reset UI
        //        _masterService.StopLiveReading(ProbeReadingHandler);

        //        MessageBox.Show("Mastering stopped.");
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Error stopping mastering: {ex.Message}");
        //    }
        //}

        // Example probe reading event handler (pass to master service)
        private void ProbeReadingHandler(object? sender, ProbeReadingEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Probe {e.ModuleId}: {e.Value:0.000}");
            });
        }




    }
}
