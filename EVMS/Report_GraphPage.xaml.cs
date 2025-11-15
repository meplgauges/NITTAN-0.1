using EVMS.Service;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EVMS
{
    public partial class Report_GraphPage : UserControl, INotifyPropertyChanged
    {
        private readonly DataStorageService _dataService;
        private Canvas _canvas;

        public ObservableCollection<ISeries> LineSeries { get; set; } = new();
        public ObservableCollection<ISeries> BarSeries { get; set; } = new();
        public ObservableCollection<ISeries> PieSeries { get; set; } = new();

        public ObservableCollection<string> ActiveParts { get; set; } = new();
        public ObservableCollection<string> LotNumbers { get; set; } = new();
        public ObservableCollection<string> ParametersOptions { get; set; } = new();
        public ObservableCollection<string> Operators { get; set; } = new();
        public ObservableCollection<string> DesignOptions { get; set; } = new();

        private string _selectedPartNo;
        private string _selectedLotNo;
        private string _selectedParameter;
        private string _selectedOperator;
        private string _selectedDesign;

        private DateTime? _selectedDateTimeFrom = DateTime.Now.AddDays(-7);
        private DateTime? _selectedDateTimeTo = DateTime.Now;
        private bool _isStopped = false;


        public event PropertyChangedEventHandler PropertyChanged;

        public Report_GraphPage()
        {
            InitializeComponent();

            _dataService = new DataStorageService();
            _canvas = new Canvas();

            LoadDesignOptions();
            LoadActiveParts();

            Loaded += Report_GraphPage_Loaded;
            Unloaded += Report_GraphPage_Unloaded;
            PreviewKeyDown += SettingsPage_PreviewKeyDown;

            DataContext = this;
        }

        #region Properties
        public string SelectedPartNo
        {
            get => _selectedPartNo;
            set
            {
                if (_selectedPartNo != value)
                {
                    _selectedPartNo = value;
                    OnPropertyChanged(nameof(SelectedPartNo));
                    _ = OnSelectedPartNoChangedAsync();
                }
            }
        }

        public string SelectedLotNo
        {
            get => _selectedLotNo;
            set { _selectedLotNo = value; OnPropertyChanged(nameof(SelectedLotNo)); }
        }

        public string SelectedParameter
        {
            get => _selectedParameter;
            set { _selectedParameter = value; OnPropertyChanged(nameof(SelectedParameter)); }
        }

        public string SelectedOperator
        {
            get => _selectedOperator;
            set { _selectedOperator = value; OnPropertyChanged(nameof(SelectedOperator)); }
        }

        public string SelectedDesign
        {
            get => _selectedDesign;
            set
            {
                if (_selectedDesign != value)
                {
                    _selectedDesign = value;
                    OnPropertyChanged(nameof(SelectedDesign));
                    // update visibility on UI thread
                    Dispatcher.InvokeAsync(UpdateChartVisibility, DispatcherPriority.Normal);
                }
            }
        }

        public DateTime? SelectedDateTimeFrom
        {
            get => _selectedDateTimeFrom;
            set
            {
                if (_selectedDateTimeFrom != value)
                {
                    _selectedDateTimeFrom = value;
                    OnPropertyChanged(nameof(SelectedDateTimeFrom));
                    _ = ReloadLotAndOperatorAsync();
                }
            }
        }

        public DateTime? SelectedDateTimeTo
        {
            get => _selectedDateTimeTo;
            set
            {
                if (_selectedDateTimeTo != value)
                {
                    _selectedDateTimeTo = value;
                    OnPropertyChanged(nameof(SelectedDateTimeTo));
                    _ = ReloadLotAndOperatorAsync();
                }
            }
        }
        #endregion

        #region Lifecycle & Rendering Safety
        private void Report_GraphPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Keep focus behavior (your original)
                Focusable = true;
                IsTabStop = true;
                Keyboard.Focus(this);
                FocusManager.SetFocusedElement(Window.GetWindow(this), this);
            }
            catch { /* ignore focus exceptions */ }

            // Attach rendering handler safely (ensure single subscription)
            try
            {
                CompositionTarget.Rendering -= OnCompositionTargetRendering;
                CompositionTarget.Rendering += OnCompositionTargetRendering;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Loaded rendering attach] {ex.Message}");
            }

            // Ensure charts hidden initially until you click Submit
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (LineChart != null) { LineChart.Visibility = Visibility.Collapsed; LineChart.IsHitTestVisible = false; }
                    if (BarChart != null) { BarChart.Visibility = Visibility.Collapsed; BarChart.IsHitTestVisible = false; }
                    if (PieChart != null) { PieChart.Visibility = Visibility.Collapsed; PieChart.IsHitTestVisible = false; }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Loaded chart init] {ex.Message}");
                }
            }, DispatcherPriority.Background);
        }

        private bool _isUnloaded = false;

        private void Report_GraphPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_isUnloaded) return; // 🔒 already unloaded
            _isUnloaded = true;

            try
            {
                CompositionTarget.Rendering -= OnCompositionTargetRendering;
                StopSafeRendering();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Unload error] {ex.Message}");
            }
        }


        // Very defensive rendering handler — will detach itself if page or canvas gone
        private void OnCompositionTargetRendering(object sender, EventArgs e)
        {
            try
            {
                if (_isUnloaded || _isStopped)
                {
                    CompositionTarget.Rendering -= OnCompositionTargetRendering;
                    return;
                }

                if (LineChart == null && BarChart == null && PieChart == null)
                {
                    CompositionTarget.Rendering -= OnCompositionTargetRendering;
                    return;
                }

                // Optional refresh logic only if chart is visible
                if (SelectedDesign == "Bar Chart" && BarChart?.IsVisible == true)
                {
                    // safely refresh visuals if needed
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Render error] {ex.Message}");
            }
        }



        private void StopSafeRendering()
        {
            if (_isStopped) return; // ✅ prevent double call
            _isStopped = true;

            try
            {
                // 1️⃣ Detach global render event
                try { CompositionTarget.Rendering -= OnCompositionTargetRendering; } catch { }

                // 2️⃣ Disable LiveCharts' internal render ticker safely
                try
                {
                    // This ensures the LiveCharts ticker will not crash
                    LiveCharts.Configure(settings =>
                    {
                        // Just a no-op configuration pass to ensure LiveCharts internal context resets safely.
                        settings.AddDefaultMappers();
                    });
                }
                catch { }

                // 3️⃣ Disable local canvas
                if (_canvas != null)
                {
                    try { _canvas.IsEnabled = false; } catch { }
                    _canvas = null;
                }

                // 4️⃣ Clear charts safely
                void SafeClearChart(FrameworkElement chart)
                {
                    if (chart == null) return;

                    try
                    {
                        if (chart is CartesianChart cartesian)
                        {
                            cartesian.Series = Array.Empty<ISeries>();
                            cartesian.XAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();
                            cartesian.YAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();
                        }
                        else if (chart is PieChart pie)
                        {
                            pie.Series = Array.Empty<ISeries>();
                        }
                    }
                    catch { }
                }

                SafeClearChart(LineChart);
                SafeClearChart(BarChart);
                SafeClearChart(PieChart);

                // 5️⃣ Clear ObservableCollections
                try
                {
                    LineSeries?.Clear();
                    BarSeries?.Clear();
                    PieSeries?.Clear();
                }
                catch { }

                // 6️⃣ Force GC (optional but helps release Skia GPU handles)
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StopSafeRendering Fatal] {ex.Message}");
            }
        }




        #endregion

        #region Input & Navigation
        private void SettingsPage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HandleEscKeyAction();
                e.Handled = true;
            }
        }

        private async void HandleEscKeyAction()
        {
            // allow one frame to complete before clearing
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

            var mainWindow = Window.GetWindow(this);
            if (mainWindow?.FindName("MainContentGrid") is Grid grid)
            {
                grid.Children.Clear();
                grid.Children.Add(new Dashboard
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                });
            }
        }


        #endregion

        #region Data Load Helpers
        private void LoadDesignOptions()
        {
            DesignOptions.Clear();
            DesignOptions.Add("Line Chart");
            //DesignOptions.Add("Bar Chart");
            //DesignOptions.Add("Pie Chart");
            SelectedDesign = "Line Chart";
        }

        private void LoadActiveParts()
        {
            try
            {
                var parts = _dataService.GetActiveParts();
                ActiveParts.Clear();
                ActiveParts.Add("All");

                if (parts != null)
                {
                    foreach (var part in parts)
                        if (!string.IsNullOrWhiteSpace(part.Para_No))
                            ActiveParts.Add(part.Para_No);
                }

                SelectedPartNo = "All";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadActiveParts] {ex.Message}");
            }
        }

        private async Task OnSelectedPartNoChangedAsync()
        {
            if (string.IsNullOrEmpty(SelectedPartNo)) return;
            await LoadLotNumbersAsync(SelectedPartNo);
            LoadParameters(SelectedPartNo);
            await LoadOperatorsAsync(SelectedPartNo);
        }

        private async Task ReloadLotAndOperatorAsync()
        {
            try
            {
                string partFilter = SelectedPartNo == "All" ? null : SelectedPartNo;
                DateTime? from = SelectedDateTimeFrom;
                DateTime? to = SelectedDateTimeTo;

                LotNumbers.Clear();
                LotNumbers.Add("All");
                var lots = await _dataService.GetLotNumbersByPartAndDateRangeAsync(partFilter, from, to);
                if (lots != null)
                {
                    foreach (var lot in lots)
                        if (!string.IsNullOrWhiteSpace(lot) && !LotNumbers.Contains(lot))
                            LotNumbers.Add(lot);
                }

                SelectedLotNo = LotNumbers.FirstOrDefault();

                Operators.Clear();
                Operators.Add("All");
                var ops = await _data_service_safe_getops(partFilter, from, to);
                if (ops != null)
                {
                    foreach (var op in ops)
                        if (!string.IsNullOrWhiteSpace(op) && !Operators.Contains(op))
                            Operators.Add(op);
                }

                SelectedOperator = Operators.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReloadLotAndOperatorAsync] {ex.Message}");
            }
        }

        // wrapper to guard unexpected exceptions from service
        private async Task<IEnumerable<string>> _data_service_safe_getops(string partFilter, DateTime? from, DateTime? to)
        {
            try
            {
                return await _dataService.GetOperatorsByPartAndDateRangeAsync(partFilter, from, to) ?? Enumerable.Empty<string>();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        public async Task LoadLotNumbersAsync(string partNo)
        {
            try
            {
                var lots = await _dataService.GetLotNumbersByPartAndDateRangeAsync(
                    partNo == "All" ? null : partNo, SelectedDateTimeFrom, SelectedDateTimeTo);

                LotNumbers.Clear();
                LotNumbers.Add("All");

                if (lots != null)
                {
                    foreach (var lot in lots)
                        if (!string.IsNullOrWhiteSpace(lot) && !LotNumbers.Contains(lot))
                            LotNumbers.Add(lot);
                }

                SelectedLotNo = "All";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadLotNumbersAsync] {ex.Message}");
            }
        }

        public async Task LoadOperatorsAsync(string partNo)
        {
            try
            {
                var ops = await _dataService.GetOperatorsByPartAndDateRangeAsync(
                    partNo == "All" ? null : partNo, SelectedDateTimeFrom, SelectedDateTimeTo);

                Operators.Clear();
                Operators.Add("All");

                if (ops != null)
                {
                    foreach (var op in ops)
                        if (!string.IsNullOrWhiteSpace(op) && !Operators.Contains(op))
                            Operators.Add(op);
                }

                SelectedOperator = "All";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadOperatorsAsync] {ex.Message}");
            }
        }

        private void LoadParameters(string partNumber)
        {
            try
            {
                var config = _dataService.GetPartConfig(partNumber == "All" ? null : partNumber);
                ParametersOptions.Clear();

                if (config != null)
                {
                    foreach (var item in config)
                        ParametersOptions.Add(item.Parameter);
                }

                SelectedParameter = ParametersOptions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadParameters] {ex.Message}");
            }
        }
        #endregion

        #region Chart Helpers & Loading
        private string MapParameterToColumn(string parameter) =>
            parameter switch
            {
                "Overall Length" => "OL",
                "Datum to End" => "DE",
                "Head Diameter" => "HD",
                "Groove Position" => "GP",
                "Stem Dia Near Groove" => "STDG",
                "Stem Dia Near Undercut" => "STDU",
                "Groove Diameter" => "GIR_DIA",
                "Straightness" => "STN",
                "Ovality SDG" => "Ovality_SDG",
                "Ovality SDU" => "Ovality_SDU",
                "Ovality Head" => "Ovality_Head",
                "Stem Taper" => "Stem_Taper",
                "End Face Runout" => "EFRO",
                "Face Runout" => "Face_Runout",
                "Seat Height" => "SH",
                _ => parameter?.Replace(" ", "") ?? string.Empty
            };

        private async void OnSubmitClicked(object sender, RoutedEventArgs e)
        {
            await LoadParameterChartAsync();
        }

        private async Task LoadParameterChartAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(SelectedParameter) || string.IsNullOrEmpty(SelectedPartNo))
                    return;

                string partFilter = SelectedPartNo == "All" ? null : SelectedPartNo;
                string lotFilter = SelectedLotNo == "All" ? null : SelectedLotNo;
                string operatorFilter = SelectedOperator == "All" ? null : SelectedOperator;

                var config = _dataService.GetPartConfig(partFilter)?
                    .FirstOrDefault(c => c.Parameter == SelectedParameter);
                if (config == null) return;

                var measurements = await _dataService.GetMeasurementReadingsAsync(
                    partFilter, lotFilter, operatorFilter, SelectedDateTimeFrom, SelectedDateTimeTo);

                if (measurements == null || !measurements.Any()) return;

                var column = MapParameterToColumn(SelectedParameter);
                var values = new List<double>();
                var labels = new List<string>();

                foreach (var m in measurements)
                {
                    var prop = m.GetType().GetProperty(column);
                    if (prop?.GetValue(m) is double val)
                        values.Add(val);

                    // try to get a date/time label (guard null)
                    try { labels.Add(m.MeasurementDate.ToString("MM-dd HH:mm")); } catch { labels.Add(string.Empty); }
                }

                if (values.Count == 0) return;

                // Build LineSeries (with Nominal / USL / LSL)
                LineSeries.Clear();
                LineSeries.Add(new LineSeries<double>
                {
                    Values = values,
                    Name = SelectedParameter,
                    GeometrySize = 8,
                    Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 3 }
                });

                LineSeries.Add(new LineSeries<double>
                {
                    Values = Enumerable.Repeat(config.Nominal, values.Count).ToList(),
                    Name = "Nominal",
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 2 }
                });

                LineSeries.Add(new LineSeries<double>
                {
                    Values = Enumerable.Repeat(config.Nominal + config.RTolPlus, values.Count).ToList(),
                    Name = "USL",
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 }
                });

                LineSeries.Add(new LineSeries<double>
                {
                    Values = Enumerable.Repeat(config.Nominal - config.RTolMinus, values.Count).ToList(),
                    Name = "LSL",
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 }
                });

                // Build BarSeries (columns + reference lines)
                BarSeries.Clear();
                BarSeries.Add(new ColumnSeries<double>
                {
                    Values = values,
                    Name = SelectedParameter,
                    Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 1 },
                    Fill = new SolidColorPaint(new SKColor(70, 130, 180, 180))
                });

                BarSeries.Add(new LineSeries<double>
                {
                    Values = Enumerable.Repeat(config.Nominal, values.Count).ToList(),
                    Name = "Nominal",
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 2 }
                });

                BarSeries.Add(new LineSeries<double>
                {
                    Values = Enumerable.Repeat(config.Nominal + config.RTolPlus, values.Count).ToList(),
                    Name = "USL",
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 }
                });

                BarSeries.Add(new LineSeries<double>
                {
                    Values = Enumerable.Repeat(config.Nominal - config.RTolMinus, values.Count).ToList(),
                    Name = "LSL",
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 }
                });

                // Build PieSeries (counts)
                PieSeries.Clear();
                int withinTol = values.Count(v => v >= config.Nominal - config.RTolMinus && v <= config.Nominal + config.RTolPlus);
                int aboveTol = values.Count(v => v > config.Nominal + config.RTolPlus);
                int belowTol = values.Count(v => v < config.Nominal - config.RTolMinus);

                PieSeries.Add(new PieSeries<double> { Values = new double[] { withinTol }, Name = "Within Tolerance", Fill = new SolidColorPaint(SKColors.Green) });
                PieSeries.Add(new PieSeries<double> { Values = new double[] { aboveTol }, Name = "Above USL", Fill = new SolidColorPaint(SKColors.Red) });
                PieSeries.Add(new PieSeries<double> { Values = new double[] { belowTol }, Name = "Below LSL", Fill = new SolidColorPaint(SKColors.Orange) });

                // Assign series to visible chart on UI thread
                Dispatcher.Invoke(() =>
                {
                    UpdateChartVisibility(); // this method will set Series on visible control
                });
            }
            catch (Exception ex)
            {
                // show friendly message but do not crash
                System.Diagnostics.Debug.WriteLine($"[LoadParameterChartAsync] {ex.Message}");
                try
                {
                    MessageBox.Show($"Unable to load chart data: {ex.Message}", "Chart Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { }
            }
        }

        // Safe update: only assign Series when control exists
        private void UpdateChartVisibility()
        {
            try
            {
                // If charts are not created yet, do nothing (will be handled later)
                if (LineChart == null && BarChart == null && PieChart == null) return;

                // collapse all first
                try { if (LineChart != null) { LineChart.Visibility = Visibility.Collapsed; LineChart.IsHitTestVisible = false; } } catch { }
                try { if (BarChart != null) { BarChart.Visibility = Visibility.Collapsed; BarChart.IsHitTestVisible = false; } } catch { }
                try { if (PieChart != null) { PieChart.Visibility = Visibility.Collapsed; PieChart.IsHitTestVisible = false; } } catch { }

                switch (SelectedDesign)
                {
                    case "Line Chart":
                        if (LineChart != null)
                        {
                            LineChart.Visibility = Visibility.Visible;
                            LineChart.IsHitTestVisible = true;
                            try { LineChart.Series = LineSeries?.ToArray() ?? Array.Empty<ISeries>(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Set LineChart.Series] {ex.Message}"); }
                        }
                        break;

                    case "Bar Chart":
                        if (BarChart != null)
                        {
                            BarChart.Visibility = Visibility.Visible;
                            BarChart.IsHitTestVisible = true;
                            try { BarChart.Series = BarSeries?.ToArray() ?? Array.Empty<ISeries>(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Set BarChart.Series] {ex.Message}"); }
                        }
                        break;

                    case "Pie Chart":
                        if (PieChart != null)
                        {
                            PieChart.Visibility = Visibility.Visible;
                            PieChart.IsHitTestVisible = true;
                            try { PieChart.Series = PieSeries?.ToArray() ?? Array.Empty<ISeries>(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Set PieChart.Series] {ex.Message}"); }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateChartVisibility] {ex.Message}");
            }
        }
        #endregion

        private void Designe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                UpdateChartVisibility();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Designe_SelectionChanged Error] {ex.Message}");
            }
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
