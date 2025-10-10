
using EVMS.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using static EVMS.Service.MasterService;

namespace EVMS
{
    public partial class ResultPage : UserControl
    {
        public event Action<string>? StatusMessageChanged;

        private Dictionary<string, UserControl> _progressBarControls = new Dictionary<string, UserControl>();
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool useFirstDesign = true;
        private bool _showLeft = true;
        private List<PartReadingDataModel> parameterData;
        private string activePartNumber = string.Empty;

        private PlcProbeService plcProbeService;
        private DataStorageService dataStorageService;
        private MasterService _masterService;

        private string _model;
        private string _lotNo;
        private string _userId;


        public ResultPage(string model, string lotNo, string userId)
        {
            InitializeComponent();
            _model = model;
            _lotNo = lotNo;
            _userId = userId;

            SetData(_model, _lotNo, _userId);
            this.Loaded += ResultPage_Loaded;
            this.Unloaded += ResultPage_Unloaded;

            dataStorageService = new DataStorageService();
            _masterService = new MasterService();
            plcProbeService = new PlcProbeService();

            _masterService.CalculatedValuesWithStatusReady += MasterService_CalculatedValuesWithStatusReady;

            _masterService.StatusMessageUpdated += (message) =>
            {
                StatusMessageChanged?.Invoke(message);
            };

            // Set DataContext for data binding
            this.DataContext = this;

            // Initialize counts to zero to display correctly on UI load
            InspectionQty = 0;
            OkCount = 0;
            NgCount = 0;
        }

     

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetData(string model, string lotNo, string userId)
        {
            txtModel.Text = model;
            txtLotNo.Text = lotNo;
            txtUserId.Text = userId;
        }

        private void NotifyStatus(string message)
        {
            StatusMessageChanged?.Invoke(message);
        }

        private async void MasterService_CalculatedValuesWithStatusReady(object? sender, Dictionary<string, ParameterResult> resultsWithStatus)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgressBarsWithStatus(resultsWithStatus);

                int okCount = resultsWithStatus.Count(r => r.Value.IsOk);
                int ngCount = resultsWithStatus.Count - okCount;
                int totalCount = resultsWithStatus.Count;

                InspectionQty = totalCount;
                OkCount = okCount;
                NgCount = ngCount;

                string status = okCount == totalCount ? "OK" : "NG";
                //NotifyStatus($"Inspection Completed. OK: {okCount}, NG: {ngCount}");

                // Save inspection asynchronously
                _ = Task.Run(async () =>
                {
                    float GetValue(string key) => resultsWithStatus.TryGetValue(key, out var param) ? (float)param.Value : 0f;

                    await dataStorageService.InsertMasterInspectionAsync(
                        _model,
                        _userId,
                        _lotNo,
                        GetValue("OL"),
                        GetValue("DE"),
                        GetValue("HD"),
                        GetValue("GP"),
                        GetValue("STDG"),
                        GetValue("STDU"),
                        GetValue("GIR DIA"),
                        GetValue("STN"),
                        GetValue("EFRO"),
                        GetValue("SH"),
                        GetValue("S R/O"),
                        GetValue("DG"),
                        status
                    );
                });
            });
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
                    NotifyStatus("PLC and Probe Connected Successfully");
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

        // Helper method to add timeout to PLC connection
        



        private void ResultPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _masterService.Dispose();
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



        // Dictionary to hold references to dynamically created progress bar controls keyed by parameter name

        /// <summary>
        /// Dynamically load progress bars into ProgressBarContainer based on parameterData.
        /// Tracks controls in _progressBarControls dictionary for later value updates.
        /// </summary>
        private void LoadProgressBars()
        {
            ProgressBarContainer.Children.Clear();
            _progressBarControls.Clear();

            foreach (var param in parameterData)
            {
                UserControl progressBar;

                double min = param.Nominal - param.RTolMinus;
                double max = param.Nominal + param.RTolPlus;
                double mean = param.Nominal;


                if (useFirstDesign)
                {
                    var pb = new ResultProgressBar { Margin = new Thickness(5) };
                    pb.ParameterName = param.Parameter;
                    pb.MinValue = min;
                    pb.MaxValue = max;
                    pb.MeanValue = mean;

                    pb.Value = 0; // Initialize with zero or default
                    progressBar = pb;
                }
                else
                {
                    var pb = new ProgresBarControl { Margin = new Thickness(5) };
                    pb.Min = min;
                    pb.Mean = mean;
                    pb.Max = max;
                    pb.Value = 0; // Initialize with zero or default
                    pb.Title = param.Parameter;
                    progressBar = pb;
                }

                ProgressBarContainer.Children.Add(progressBar);
                _progressBarControls[param.Parameter] = progressBar;
            }
        }

        /// <summary>
        /// Update the progress bars with mastered values after mastering completes.
        /// Input dictionary keys must match Parameter names used when loading progress bars.
        /// </summary>
        /// <param name="masteredValues">Dictionary of parameter name to mastered reference value</param>
        //private void UpdateProgressBarsWithCalculatedValues(Dictionary<string, double> calculatedValues)
        //{
        //    foreach (var kvp in calculatedValues)
        //    {
        //        if (_progressBarControls.TryGetValue(kvp.Key, out var control))
        //        {
        //            if (control is ResultProgressBar pb1)
        //            {
        //                pb1.Value = kvp.Value;  // You might need to add an ActualValue property in your control
        //            }
        //            else if (control is ProgresBarControl pb2)
        //            {
        //                pb2.Value = kvp.Value;  // Likewise
        //            }
        //        }
        //    }
        //}

        private void UpdateProgressBarsWithStatus(Dictionary<string, ParameterResult> resultsWithStatus)
        {
            foreach (var kvp in resultsWithStatus)
            {
                if (_progressBarControls.TryGetValue(kvp.Key, out var control))
                {
                    if (control is ResultProgressBar pb)
                    {
                        pb.UpdateValue(kvp.Value.Value, kvp.Value.IsOk);
                    }
                    else if (control is ProgresBarControl pb2)
                    {
                        pb2.Value = kvp.Value.Value;
                        // Optional: add IsOk property and color logic to ProgresBarControl if desired
                    }
                }
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

        // Mastering toggle
        private async void MasterToggle_Checked(object sender, RoutedEventArgs e)
        {
            ToggleButton? toggleButton = sender as ToggleButton;
            if (toggleButton == null) return;

            toggleButton.IsEnabled = false;

            try
            {
                _masterService.IsMasteringStage = true;

                // Run mastering procedure
                await _masterService.MasterCheckProcedureAsync();

                // Mastering complete → automatically turn off the toggle
                _masterService.IsMasteringStage = false;
                toggleButton.IsChecked = false;

                //MessageBox.Show("Mastering completed. You can now perform Master Inspection.",
                //                "Mastering Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting mastering: {ex.Message}");
                toggleButton.IsChecked = false;
            }
            finally
            {
                toggleButton.IsEnabled = true;
            }
        }

        // Master Inspection toggle
        private async void MasterInspectionToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton inspectionToggle)) return;

            if (!_masterService.IsConnected)
            {
                MessageBox.Show("PLC not connected. Cannot perform inspection.");
                inspectionToggle.IsChecked = false;
                return;
            }

            //if (!_masterService.MasterComplete)
            //{
            //    MessageBox.Show("Mastering not complete. Please complete mastering first.");
            //    inspectionToggle.IsChecked = false;
            //    return;
            //}

            //inspectionToggle.IsEnabled = false;

            try
            {
                _masterService.IsMasteringStage = false;

                // Run the inspection procedure
                await _masterService.MasterCheckProcedureAsync();

                // Inspection complete → turn off toggle automatically
                inspectionToggle.IsChecked = false;
                //MessageBox.Show("Master Inspection completed.", "Inspection Completed",
                //                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during inspection: {ex.Message}");
                inspectionToggle.IsChecked = false;
            }
            finally
            {
                inspectionToggle.IsEnabled = true;
            }
        }

        private void ProbeReadingHandler(object? sender, ProbeReadingEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Probe {e.ModuleId}: {e.Value:0.000}");
            });
        }




        private int _inspectionQty;
        public int InspectionQty
        {
            get => _inspectionQty;
            set { _inspectionQty = value; OnPropertyChanged(nameof(InspectionQty)); }
        }

        private int _okCount;
        public int OkCount
        {
            get => _okCount;
            set { _okCount = value; OnPropertyChanged(nameof(OkCount)); }
        }

        private int _ngCount;
        public int NgCount
        {
            get => _ngCount;
            set { _ngCount = value; OnPropertyChanged(nameof(NgCount)); }
        }

        public void AddPartInspectionResults(Dictionary<string, ParameterResult> parameterResults)
        {
            InspectionQty++; // increment total parts inspected

            bool partIsOk = parameterResults.All(r => r.Value.IsOk);
            if (partIsOk)
                OkCount++;
            else
                NgCount++;
        }


    }
}
