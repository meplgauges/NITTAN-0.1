
using EVMS.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using static EVMS.Service.MasterService;

namespace EVMS
{
    public partial class ResultPage : UserControl
    {
        public event Action<string>? StatusMessageChanged;

        private Dictionary<string, UserControl> _progressBarControls = new Dictionary<string, UserControl>();
        public event PropertyChangedEventHandler? PropertyChanged;
        private Dictionary<string, string> fullToShortMap;
        private ProcedureMode _currentMode;
        public event EventHandler Closed;
        private DispatcherTimer shiftTimer;





        private bool useFirstDesign = true;
        private bool _showLeft = false;
        private List<PartReadingDataModel> parameterData;
        private string activePartNumber = string.Empty;
        private DataTable _measurementDataTable;
        private const int MaxRows = 10; // Show only last 10 cycles


        private PlcProbeService plcProbeService;
        private DataStorageService dataStorageService;
        private MasterService _masterService;


        public string PartNo { get; set; }
        public string LotNo { get; set; }
        public string OperatorID { get; set; }

        private string _model;
        private string _lotNo;
        private string _userId;


        public ResultPage(string model, string lotNo, string userId)
        {
            InitializeComponent();

            StartShiftTimer();

            _model = model;
            _lotNo = lotNo;
            _userId = userId;

            SetData(_model, _lotNo, _userId);
            this.Loaded += AutoManual_Loaded;

            this.Loaded += ResultPage_Loaded;
            this.Unloaded += ResultPage_Unloaded;
            this.Loaded += UserControl_Loaded;


            dataStorageService = new DataStorageService();
            _masterService = new MasterService();
            plcProbeService = new PlcProbeService();

            _masterService.CalculatedValuesWithStatusReady += MasterService_CalculatedValuesWithStatusReady;
            //_masterService.MeasurementCycleReset += OnCycleReset;


            _masterService.StatusMessageUpdated += (message) =>
            {
                StatusMessageChanged?.Invoke(message);
            };

            // Set DataContext for data binding
            this.DataContext = this;

            // Initialize counts to zero to display correctly on UI load
            //InspectionQty = 0;
            //OkCount = 0;
            //NgCount = 0;
        }
        //public class ParameterResult
        //{
        //    public double Value { get; set; }
        //    public bool IsOk { get; set; }
        //}

        private void OnCycleReset()
        {
            // Ensure this runs on the UI thread
            Dispatcher.Invoke(() =>
            {
                ResetMeasurementFieldsAndProgressBars();
            });
        }

        private async Task LoadAndDisplayInspectionDataAsync()
        {
            var existingRecord = await dataStorageService.SelectInspectionDataAsync(_model, _lotNo, _userId);

            if (existingRecord != null)
            {
                txtModel.Text = existingRecord.PartNo;
                txtLotNo.Text = existingRecord.LotNo;
                txtUserId.Text = existingRecord.OperatorID;
                txtInspectionQty.Text = existingRecord.InspectionQty.ToString();
                txtOkCount.Text = existingRecord.OkCount.ToString();

                int ngCount = existingRecord.InspectionQty - existingRecord.OkCount;
                txtNgCount.Text = ngCount.ToString();
            }
            else
            {
                // Insert a new record with zero counts if not exists
                await dataStorageService.InsertInspectionDataAsync(_model, _lotNo, _userId);

                // Display initial zero data
                txtModel.Text = _model;
                txtLotNo.Text = _lotNo;
                txtUserId.Text = _userId;
                txtInspectionQty.Text = "0";
                txtOkCount.Text = "0";
                txtNgCount.Text = "0";
            }
        }

        private async Task UpdateInspectionDataAsync()
        {
            if (int.TryParse(txtInspectionQty.Text, out int inspectionQty)
                && int.TryParse(txtOkCount.Text, out int okCount))
            {
                await dataStorageService.UpdateInspectionCountsAsync(_model, _lotNo, _userId, inspectionQty, okCount);
            }
            else
            {
                // Handle parse error if necessary
            }
        }


        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAndDisplayInspectionDataAsync();
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


        private void MasterService_CalculatedValuesWithStatusReady(object? sender, Dictionary<string, ParameterResult> resultsWithStatus)
        {
            Dispatcher.Invoke(() =>
            {
                // --- Update UI immediately with the latest inspection data ---
                UpdateProgressBarsWithStatus(resultsWithStatus);
                UpdateMeasurementFields(resultsWithStatus);

                var latestValues = resultsWithStatus.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
                LoadDataGrid(latestValues, resultsWithStatus); // 👈 new method with NG coloring

                // ✅ Count per part
                UpdateInspectionCounts(resultsWithStatus);


                string status = resultsWithStatus.All(r => r.Value.IsOk) ? "OK" : "NG";
                Debug.WriteLine($"Parts inspected: {InspectionQty}, OK: {OkCount}, NG: {NgCount}");

                // --- Save data asynchronously ---
                // --- Save data asynchronously with exclusive branching ---
                _ = Task.Run(async () =>
                {
                    try
                    {
                        float GetValue(string key) => resultsWithStatus.TryGetValue(key, out var param) ? (float)param.Value : 0f;

                        if (_currentMode == ProcedureMode.MasterInspection)
                        {
                            await dataStorageService.InsertMasterInspectionAsync(
                                _model, _userId, _lotNo,
                                GetValue("Overall Length"), GetValue("Datum to End"), GetValue("Head Diameter"),
                                GetValue("Groove Position"), GetValue("Stem Dia Near Groove"), GetValue("Stem Dia Near Undercut"),
                                GetValue("Groove Diameter"), GetValue("Straightness"), GetValue("Ovality SDG"),
                                GetValue("Ovality SDU"), GetValue("Ovality Head"), GetValue("Stem Taper"),
                                GetValue("End Face Runout"), GetValue("Face Runout"), GetValue("Seat Height"),
                                GetValue("Seat Runout"), GetValue("Datum to Groove"), status);
                        }
                        else if (_currentMode == ProcedureMode.Measurement)
                        {
                            await dataStorageService.InsertMeasurementReadingAsync(
                                _model, _userId, _lotNo,
                                GetValue("Overall Length"), GetValue("Datum to End"), GetValue("Head Diameter"),
                                GetValue("Groove Position"), GetValue("Stem Dia Near Groove"), GetValue("Stem Dia Near Undercut"),
                                GetValue("Groove Diameter"), GetValue("Straightness"), GetValue("Ovality SDG"),
                                GetValue("Ovality SDU"), GetValue("Ovality Head"), GetValue("Stem Taper"),
                                GetValue("End Face Runout"), GetValue("Face Runout"), GetValue("Seat Height"),
                                GetValue("Seat Runout"), GetValue("Datum to Groove"), status);
                        }
                        // No save if neither mode is active
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ Error saving results: {ex.Message}");
                    }
                });


                // ✅ NEW: Clear values automatically after 1 second
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1500); // wait 1 second
                    Dispatcher.Invoke(() =>
                    {
                        ResetMeasurementFieldsAndProgressBars();
                    });
                });
            });
        }


        private void UpdateInspectionCounts(Dictionary<string, ParameterResult> resultsWithStatus)
        {
            if (_currentMode == ProcedureMode.MasterInspection)
            {
                // Skip incrementing counts during Master Inspection
                return;
            }
            Dispatcher.Invoke(() =>
            {
                // Increment InspectionQty correctly
                int inspected;
                if (!int.TryParse(txtInspectionQty.Text, out inspected))
                    inspected = 0;
                inspected++;
                InspectionQty = inspected; // This updates the backing field and raises OnPropertyChanged
                txtInspectionQty.Text = inspected.ToString();

                // Increment OkCount or NgCount depending on inspection status
                bool allOk = resultsWithStatus.All(r => r.Value.IsOk);
                if (allOk)
                {
                    int okCount;
                    if (!int.TryParse(txtOkCount.Text, out okCount))
                    okCount = 0;
                    okCount++;
                    OkCount = okCount;
                    txtOkCount.Text = okCount.ToString();
                }
                else
                {
                    int ngCount;
                    if (!int.TryParse(txtNgCount.Text, out ngCount))
                        ngCount = 0;
                    ngCount++;
                    NgCount = ngCount;
                    txtNgCount.Text = ngCount.ToString();
                }

                currentMasterCount++;


                // After incrementing counts, call master expiration check
                CheckMasterExpirationDuringMeasurement();
            });
        }







        private void AutoManual_Loaded(object sender, RoutedEventArgs e)
        {
            // Load saved mode from DB and set toggle button state
            int modeBit = dataStorageService.GetAutoManualBit();
            AutoManualToggle.IsChecked = (modeBit == 1);
        }

        private async void ResultPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focusable = true;
            this.Focus();

            this.PreviewKeyDown += ResultPage_PreviewKeyDown;
            InitializeValveDataAndUI();
            InitializeDataGrid();
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

                fullToShortMap = parameterData.ToDictionary(p => p.Parameter, p => p.ShortName ?? p.Parameter);
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
            e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            e.Column.HeaderStyle = headerStyle;
        }



        private void InitializeDataGrid()
        {
            _measurementDataTable = new DataTable();

            // Add serial number column
            _measurementDataTable.Columns.Add("No", typeof(int));

            // Assuming parameterData contains the list from the DB with parameter and ShortName info
            foreach (var param in parameterData)
            {
                string columnName = param.ShortName;  // use ShortName
                _measurementDataTable.Columns.Add(columnName, typeof(string));
            }


            ValveReadingsGrid.AddHandler(DataGridColumnHeader.ClickEvent,
                new RoutedEventHandler(DataGridColumnHeader_Click));

            // Start with empty DataGrid (no rows)
            ValveReadingsGrid.ItemsSource = _measurementDataTable.DefaultView;
        }


        private void DataGridColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is DataGridColumnHeader header && header.Column != null)
            {
                string parameterName = header.Column.Header.ToString();
                if (parameterName == "No") return;

                var values = GetLast10NumericValues(parameterName);
                //if (values.Count == 0)
                //{
                //    MessageBox.Show($"No readings available for {parameterName}.",
                //        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                //    return;
                //}

                // Show big custom window
                var win = new VariationWindow(parameterName, values)
                {
                    Owner = Window.GetWindow(ValveReadingsGrid)
                };
                win.ShowDialog();
            }
        }




        private List<double> GetLast10NumericValues(string parameterName)
        {
            if (_measurementDataTable == null || !_measurementDataTable.Columns.Contains(parameterName))
                return new List<double>();

            return _measurementDataTable.AsEnumerable()
                .Take(10)
                .Select(r =>
                {
                    double val;
                    return double.TryParse(r[parameterName]?.ToString(), out val) ? val : double.NaN;
                })
                .Where(v => !double.IsNaN(v))
                .ToList();
        }




        private int _globalSerialCounter = 1; // Initialize in your class (not in method)

        private void LoadDataGrid(Dictionary<string, double> latestValues, Dictionary<string, ParameterResult> resultsWithStatus)
        {
            if (_measurementDataTable == null || latestValues == null) return;

            Dispatcher.Invoke(() =>
            {
                var row = _measurementDataTable.NewRow();
                row["No"] = _globalSerialCounter++;

                // Assign data using full-to-short name map to ensure values map to correct short-named columns
                foreach (var kvp in latestValues) // kvp.Key = full parameter name
                {
                    if (fullToShortMap.TryGetValue(kvp.Key, out string shortName) &&
                        _measurementDataTable.Columns.Contains(shortName))
                    {
                        row[shortName] = kvp.Value.ToString("F3");
                    }
                }

                // Fill any columns not assigned with empty string
                foreach (DataColumn col in _measurementDataTable.Columns)
                {
                    if (col.ColumnName == "No") continue;
                    if (row[col.ColumnName] == DBNull.Value)
                    {
                        row[col.ColumnName] = "";
                    }
                }

                _measurementDataTable.Rows.InsertAt(row, 0);

                while (_measurementDataTable.Rows.Count > MaxRows) // MaxRows = 10 in your code
                    _measurementDataTable.Rows.RemoveAt(_measurementDataTable.Rows.Count - 1);

                // No need to reassign ItemsSource repeatedly - just update layout
                ValveReadingsGrid.UpdateLayout();

                // Highlight cells with NG status in red
                ValveReadingsGrid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    int rowIndex = 0; // Only color newest row

                    foreach (var kvp in fullToShortMap)
                    {
                        string shortHeader = kvp.Value;
                        var column = ValveReadingsGrid.Columns.FirstOrDefault(c => c.Header.ToString() == shortHeader);
                        if (column == null) continue;

                        var cellContent = column.GetCellContent(ValveReadingsGrid.Items[rowIndex]);
                        if (cellContent == null) continue;

                        var cell = FindParent<DataGridCell>(cellContent);
                        if (cell == null) continue;

                        // Default color (reset first)
                        cell.Foreground = Brushes.Black;

                        // Now apply red if NG
                        if (resultsWithStatus.TryGetValue(kvp.Key, out var result) && !result.IsOk)
                        {
                            cell.Foreground = Brushes.Red;
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);

            });
        }


        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
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
        private ToggleButton? _activeToggleButton = null;

        // Tracks the currently active toggle

        // Enable all toggles
        private void EnableAllToggles()
        {
            MasterToggle.IsEnabled = true;
            MasterInspectionToggleButton.IsEnabled = true;
            MeasurementToggle.IsEnabled = true;

            // Re-enable Auto/Manual toggle only when no operation is active
            AutoManualToggle.IsEnabled = true;
        }

        // Disable all toggles except the active one
        private void DisableOtherToggles(ToggleButton active)
        {
            MasterToggle.IsEnabled = (active == MasterToggle);
            MasterInspectionToggleButton.IsEnabled = (active == MasterInspectionToggleButton);
            MeasurementToggle.IsEnabled = (active == MeasurementToggle);

            // Disable Auto/Manual toggle whenever an operation toggle is active
            AutoManualToggle.IsEnabled = false;
        }

        // ========================= MASTER TOGGLE =========================
        private async void MasterToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton toggleButton)) return;

            if (_activeToggleButton != null && _activeToggleButton != toggleButton)
            {
                MessageBox.Show("Please turn off the other active operation before starting this.",
                                "Operation Active", MessageBoxButton.OK, MessageBoxImage.Warning);
                toggleButton.IsChecked = false;
                return;
            }

            StartBitMatchCheck(); // Start monitoring Auto/Manual bit after starting measurement
            _activeToggleButton = toggleButton;
            DisableOtherToggles(toggleButton);

            try
            {
                ResetAllResult();
                _masterService.IsMasteringStage = true;
                _masterService._continueMeasurement = false;
                ResetMeasurementFieldsAndProgressBars();

                await _masterService.MasterCheckProcedureAsync(ProcedureMode.Mastering);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting mastering: {ex.Message}");
            }
            finally
            {
                toggleButton.IsChecked = false;
                _masterService._continueMeasurement = false;
                _activeToggleButton = null;
                EnableAllToggles();
            }
        }

        // ========================= MASTER INSPECTION TOGGLE =========================
        private async void MasterInspectionToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton toggleButton)) return;

            if (!_masterService.IsConnected)
            {
                MessageBox.Show("PLC not connected. Cannot perform inspection.");
                toggleButton.IsChecked = false;
                return;
            }

            if (_activeToggleButton != null && _activeToggleButton != toggleButton)
            {
                MessageBox.Show("Please turn off the other active operation before starting this.",
                                "Operation Active", MessageBoxButton.OK, MessageBoxImage.Warning);
                toggleButton.IsChecked = false;
                return;
            }
           
            StartBitMatchCheck(); // Start monitoring Auto/Manual bit after starting measurement

            _activeToggleButton = toggleButton;
            DisableOtherToggles(toggleButton);

            try
            {
                ResetAllResult();
                _masterService.IsMasteringStage = false;
                _masterService._continueMeasurement = false;
                ResetMeasurementFieldsAndProgressBars();

                await _masterService.MasterCheckProcedureAsync(ProcedureMode.MasterInspection);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during inspection: {ex.Message}");
            }
            finally
            {
                toggleButton.IsChecked = false;
                _masterService._continueMeasurement = false;
                _activeToggleButton = null;
                EnableAllToggles();
            }
        }

        // ========================= MEASUREMENT TOGGLE =========================
        private async void MeasurementToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton toggleButton)) return;

            if (!_masterService.IsConnected)
            {
                MessageBox.Show("PLC not connected. Cannot perform measurement.");
                toggleButton.IsChecked = false;
                return;
            }

            if (_activeToggleButton != null && _activeToggleButton != toggleButton)
            {
                MessageBox.Show("Please turn off the other active operation before starting this.",
                                "Operation Active", MessageBoxButton.OK, MessageBoxImage.Warning);
                toggleButton.IsChecked = false;
                return;
            }

            // Removed expiration check here per requirement
            StartBitMatchCheck(); // Start monitoring Auto/Manual bit after starting measurement
            currentMasterCount = 0;

            _activeToggleButton = toggleButton;
            DisableOtherToggles(toggleButton);


            try
            {
                _masterService.SetPlcDevice("M101", 0);
                _masterService.SetPlcDevice("M102", 0); // General rejection

                _masterService.IsMasteringStage = false;
                _masterService._continueMeasurement = true;
                ResetMeasurementFieldsAndProgressBars();

                Dispatcher.Invoke(() =>
                {
                    _measurementDataTable?.Clear();
                    _globalSerialCounter = 1;
                    
                });

                _currentMode = ProcedureMode.Measurement;

                await _masterService.MasterCheckProcedureAsync(ProcedureMode.Measurement);
                await _masterService.RunMeasurementCycleAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during measurement: {ex.Message}");
            }
            finally
            {
                toggleButton.IsChecked = false;
                _masterService._continueMeasurement = false;
                _activeToggleButton = null;
                EnableAllToggles();
            }
        }



        // ========================= TOGGLE UNCHECKED HANDLER =========================
        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton toggleButton)) return;

            // Stop measurement if Measurement toggle turned off
            if (toggleButton == MeasurementToggle)
            {
                ResetAllResult();
                _masterService.SetPlcDevice("M301", 0); // _masterService.SetPlcDevice("M10", 0); // 

                _masterService._continueMeasurement = false;

                UpdateInspectionDataAsync();

                //ResetAllPlcBits();
            }

    bitMatchCheckTimer?.Stop();

            try
            {
                _masterService.SetPlcDevice("M300", 0);
                NotifyStatus(".");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error turning off PLC devices: {ex.Message}", "PLC Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (toggleButton == _activeToggleButton)
            {
                _activeToggleButton = null;
            }

            // Re-enable all toggles including Auto/Manual
            EnableAllToggles();
        }


        private void AutoManualToggle_Checked(object sender, RoutedEventArgs e)
        {
            // Save Auto = 1 to DB when toggled on
            dataStorageService.UpdateAutoManualBit(1);
        }

        private void AutoManualToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // Save Manual = 0 to DB when toggled off
            dataStorageService.UpdateAutoManualBit(0);
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

        //public void AddPartInspectionResults(Dictionary<string, ParameterResult> parameterResults)
        //{
        //    InspectionQty++; // increment total parts inspected

        //    bool partIsOk = parameterResults.All(r => r.Value.IsOk);
        //    if (partIsOk)
        //        OkCount++;
        //    else
        //        NgCount++;
        //}


        private void UpdateMeasurementResult(TextBox textBox, double value, bool isOk)
        {
            if (textBox == null) return;

            textBox.Text = value.ToString("0.000");

            // Change background color, keep text foreground default
            textBox.Background = isOk ? Brushes.LimeGreen : Brushes.IndianRed;
        }


        private void UpdateMeasurementLabel(TextBlock label, bool isOk)
        {
            if (label == null) return;

        }

        private void UpdateFieldIfExists(Dictionary<string, ParameterResult> results, string key, TextBox textBox, TextBlock label)
        {
            if (results.TryGetValue(key, out var result))
            {
                UpdateMeasurementResult(textBox, result.Value, result.IsOk);
                UpdateMeasurementLabel(label, result.IsOk);
            }
        }


        private void UpdateMeasurementFields(Dictionary<string, ParameterResult> resultsWithStatus)
        {
            UpdateFieldIfExists(resultsWithStatus, "Groove Diameter", GrooveDiaBox, GrooveDiaLabel);
            UpdateFieldIfExists(resultsWithStatus, "Groove Position", GroovePo, GroovePoLabel);
            UpdateFieldIfExists(resultsWithStatus, "Stem Dia Near Groove", STDG, STDGLabel);
            UpdateFieldIfExists(resultsWithStatus, "Stem Dia Near Undercut", StemDiaBox, StemDiaLabel);
            UpdateFieldIfExists(resultsWithStatus, "Head Diameter", HeadDiaBox, HeadDiaLabel);
            UpdateFieldIfExists(resultsWithStatus, "Face Runout", FaceRunout, FaceRunoutLabel);
            UpdateFieldIfExists(resultsWithStatus, "Seat Height", SeatHeightBox, SeatHeightLabel);
            UpdateFieldIfExists(resultsWithStatus, "Seat Runout", SeatRunout, SeatRunoutLabel);
            UpdateFieldIfExists(resultsWithStatus, "Datum to End", DatumToEndBox, DatumToEndLabel);
            UpdateFieldIfExists(resultsWithStatus, "Datum to Groove", DatumToGrooveBox, DatumToGrooveLabel);
            UpdateFieldIfExists(resultsWithStatus, "Stem Taper", StemTaper, StemTaperLabel);
            UpdateFieldIfExists(resultsWithStatus, "Straightness", Straightness, StraightnessLabel);
            UpdateFieldIfExists(resultsWithStatus, "Overall Length", OverallLengthBox, OverallLengthLabel);
            UpdateFieldIfExists(resultsWithStatus, "End Face Runout", HeadRunout, HeadRunoutLabel);
            UpdateFieldIfExists(resultsWithStatus, "Ovality Head", HeadOver, HeadOverLabel);
        }

        private void ResetMeasurementFieldsAndProgressBars()
        {
            // Reset all measurement TextBox values and backgrounds
            Brush defaultBg = Brushes.White;

            string zeroText = "00";

            GrooveDiaBox.Text = zeroText; GrooveDiaBox.Background = defaultBg;
            GroovePo.Text = zeroText; GroovePo.Background = defaultBg;
            STDG.Text = zeroText; STDG.Background = defaultBg;
            StemDiaBox.Text = zeroText; StemDiaBox.Background = defaultBg;
            HeadDiaBox.Text = zeroText; HeadDiaBox.Background = defaultBg;
            FaceRunout.Text = zeroText; FaceRunout.Background = defaultBg;
            SeatHeightBox.Text = zeroText; SeatHeightBox.Background = defaultBg;
            SeatRunout.Text = zeroText; SeatRunout.Background = defaultBg;
            DatumToEndBox.Text = zeroText; DatumToEndBox.Background = defaultBg;
            DatumToGrooveBox.Text = zeroText; DatumToGrooveBox.Background = defaultBg;
            StemTaper.Text = zeroText; StemTaper.Background = defaultBg;
            Straightness.Text = zeroText; Straightness.Background = defaultBg;
            OverallLengthBox.Text = zeroText; OverallLengthBox.Background = defaultBg;
            HeadRunout.Text = zeroText; HeadRunout.Background = defaultBg;
            HeadOver.Text = zeroText; HeadOver.Background = defaultBg;

            // Reset progress bar controls for both designs if present
            foreach (var control in _progressBarControls.Values)
            {
                if (control is ResultProgressBar pb1)
                {
                    pb1.Value = 0;       // reset value
                    pb1.UpdateValue(0, null); // clear any color status
                }
                else if (control is ProgresBarControl pb2)
                {
                    pb2.Value = 0;       // reset value
                                         // Optionally add a method to clear IsOk/Color state in ProgresBarControl if implemented
                }
            }
        }



        private void ResultPage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // MessageBox.Show("ESC pressed in Result Page", "Key Pressed", MessageBoxButton.OK, MessageBoxImage.Information);
                _masterService._continueMeasurement = false; // Stop measurement
                e.Handled = true;
                ResetAllPlcBits();
                _masterService.Dispose();
                // Additional ESC handling logic here
                HandleEscKeyAction();
                ResetAllResult();

                NotifyStatus(".");

            }
        }



        private void HandleEscKeyAction()
        {
            Window currentWindow = Window.GetWindow(this);
            if (currentWindow != null)
            {
                // Assuming your window has a container named MainContentGrid
                var mainContentGrid = currentWindow.FindName("MainContentGrid") as Grid;
                if (mainContentGrid != null)
                {
                    mainContentGrid.Children.Clear();
                    var resultPage = new HomePage();

                    resultPage.HorizontalAlignment = HorizontalAlignment.Stretch;
                    resultPage.VerticalAlignment = VerticalAlignment.Stretch;

                    mainContentGrid.Children.Add(resultPage);
                }
            }
        }

        private void ResetAllResult()
        {
            _masterService.SetPlcDevice("M301", 0); // General rejection
            _masterService.SetPlcDevice("M302", 0); // SRO rejection
            _masterService.SetPlcDevice("M303", 0); // STDIA rejection
            _masterService.SetPlcDevice("M304", 0); // Seat Height rejection
            _masterService.SetPlcDevice("M305", 0); // Groove Diameter/Position rejection
            _masterService.SetPlcDevice("M306", 0); // Groove Diameter/Position rejection




        }


        private void ResetAllPlcBits()
        {
            string[] bitsToReset =
            {
                    "M400", "M100", "M300", "M10", "M14", "M301"
                };

            try
            {
                foreach (var bit in bitsToReset)
                {
                    _masterService.SetPlcDevice(bit, 0);
                }
            }
            catch (Exception ex)
            {
                // Show a single message for all errors
                MessageBox.Show(
                    $"PLC Reset Failed.\nError: {ex.Message}",
                    "PLC Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                // Close current window/page
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Window currentWindow = Window.GetWindow(this);
                    if (currentWindow != null)
                    {
                        currentWindow.Close();
                    }
                });
            }
        }




        private void StartShiftTimer()
        {
            shiftTimer = new DispatcherTimer();
            shiftTimer.Interval = TimeSpan.FromSeconds(1);  // checks every minute
            shiftTimer.Tick += ShiftTimer_Tick;
            shiftTimer.Start();

            UpdateShiftDisplay(); // Initial update
        }

        private void ShiftTimer_Tick(object sender, EventArgs e)
        {
            UpdateShiftDisplay();
        }

        private void UpdateShiftDisplay()
        {
            string currentShift = GetShiftCode();
            string dateShift = DateTime.Now.ToString("yyyyMMdd") + currentShift;

            // Example: TextBox named TxtShift or Label named LblShift
            txtLotNo.Text = dateShift;
        }

        private string GetShiftCode()
        {
            TimeSpan current = DateTime.Now.TimeOfDay;

            TimeSpan shiftAStart = new TimeSpan(6, 0, 0);
            TimeSpan shiftAEnd = new TimeSpan(13, 59, 59);
            TimeSpan shiftBStart = new TimeSpan(14, 0, 0);
            TimeSpan shiftBEnd = new TimeSpan(21, 59, 59);
            TimeSpan shiftCStart = new TimeSpan(22, 0, 0);
            TimeSpan shiftCEnd = new TimeSpan(5, 59, 59);

            if (current >= shiftAStart && current <= shiftAEnd)
                return "A";
            else if (current >= shiftBStart && current <= shiftBEnd)
                return "B";
            else
                return "C";
        }


        private DispatcherTimer expirationTimer1;

        /// <summary>
        /// Checks master expiration: stops on count, starts (and stops) timer for time.
        /// Call at the start of measurement and after each cycle.
        /// </summary>
        // Automatically turns off mastering when inspection count reaches expiration limit
        // Master expiration using only Count mode
        // Call this method after each inspection count update (e.g., after each measurement)
        private int currentMasterCount = 0;

        public void CheckMasterExpirationDuringMeasurement()
        {
            var (mode, masterCount, _) = dataStorageService.GetMasterExpiration();

            // Only check for count mode expiration
            if (mode == 1 )
            {
                if (currentMasterCount >= masterCount)
                {
                    // Auto-turn off measurement
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (MasterToggle.IsChecked == true)
                            MasterToggle.IsChecked = false;
                    });

                    _masterService._continueMeasurement = false;

                    MessageBox.Show(
                        "Master count limit reached. Measurement has been automatically stopped.",
                        "Master Expiration",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    // Reset the counter for next time
                    currentMasterCount = 0;
                }
            }
        }



        private DispatcherTimer bitMatchCheckTimer;
        private int? lastSoftwareBitValue = null;
        private int? lastPlcBitValue = null;
        private bool wasPreviousMismatch = false;

        private void StartBitMatchCheck()
        {
            bitMatchCheckTimer = new DispatcherTimer();
            bitMatchCheckTimer.Interval = TimeSpan.FromSeconds(1);
            bitMatchCheckTimer.Tick += BitMatchCheckTimer_Tick;
            bitMatchCheckTimer.Start();
        }

        private void BitMatchCheckTimer_Tick(object sender, EventArgs e)
        {
            var autoList = dataStorageService.GetActiveBit();
            var autoControl = autoList.FirstOrDefault(c => string.Equals(c.Description, "Auto/Manual", StringComparison.OrdinalIgnoreCase));

            if (autoControl == null)
                return; // Can't check without software bit

            int softwareBit = autoControl.Bit; // Software bit value
            int plcBit = _masterService.GetPlcDeviceBit("X14"); // PLC bit

            if (softwareBit == plcBit)
            {
                // Bits match - reset mismatch state
                wasPreviousMismatch = false;
            }
            else
            {
                // Bits do not match
                if (wasPreviousMismatch)
                {
                    // Consecutive mismatch detected - show error and stop measurement
                    bitMatchCheckTimer.Stop(); // Stop checking further

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Auto/Manual bit mismatch detected consecutively. PLC is in Manual mode or out of sync.",
                                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        if (_masterService._continueMeasurement)
                        {
                            _masterService._continueMeasurement = false;
                            if (MeasurementToggle.IsChecked == true)
                                MeasurementToggle.IsChecked = false;
                        }
                    });
                }
                else
                {
                    // First mismatch, set flag and wait next tick to confirm
                    wasPreviousMismatch = true;
                }
            }
        }

    }
}

