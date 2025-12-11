using EVMS.Service;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EVMS
{
    public partial class RnR_Report_Page : UserControl, INotifyPropertyChanged
    {
        // Use your real DataStorageService (as in Report_GraphPage)
        private readonly DataStorageService _dataService;

        // FILTER SOURCES
        public ObservableCollection<string> ActiveParts { get; } = new();
        public ObservableCollection<string> LotNumbers { get; } = new();
        public ObservableCollection<string> ParametersOptions { get; } = new();
        public ObservableCollection<string> Operators { get; } = new();

        // GRID DATA (bound to XAML)
        public ObservableCollection<RnRGridRow> Appraiser1Data { get; } = new();
        public ObservableCollection<RnRGridRow> Appraiser2Data { get; } = new();
        public ObservableCollection<RnRGridRow> Appraiser3Data { get; } = new();

        // SUMMARY PROPERTIES (bound to XAML)
        private double _repeatability;
        public double Repeatability { get => _repeatability; set { _repeatability = value; OnPropertyChanged(nameof(Repeatability)); } }

        private double _reproducibility;
        public double Reproducibility { get => _reproducibility; set { _reproducibility = value; OnPropertyChanged(nameof(Reproducibility)); } }

        private double _partVariation;
        public double PartVariation { get => _partVariation; set { _partVariation = value; OnPropertyChanged(nameof(PartVariation)); } }

        private double _totalPVPercent;
        public double TotalPVPercent { get => _totalPVPercent; set { _totalPVPercent = value; OnPropertyChanged(nameof(TotalPVPercent)); } }

        private double _totalTolerance;
        public double TotalTolerance { get => _totalTolerance; set { _totalTolerance = value; OnPropertyChanged(nameof(TotalTolerance)); } }

        private double _grrPercentage;
        public double GRRPercentage { get => _grrPercentage; set { _grrPercentage = value; OnPropertyChanged(nameof(GRRPercentage)); } }

        private string _grrConclusion;
        public string GRRConclusion { get => _grrConclusion; set { _grrConclusion = value; OnPropertyChanged(nameof(GRRConclusion)); } }

        private int _ndc;
        public int Ndc { get => _ndc; set { _ndc = value; OnPropertyChanged(nameof(Ndc)); } }

        // FILTER SELECTIONS (bound to XAML)
        private string _selectedPartNo;
        public string SelectedPartNo { get => _selectedPartNo; set { if (_selectedPartNo != value) { _selectedPartNo = value; OnPropertyChanged(nameof(SelectedPartNo)); _ = OnSelectedPartNoChangedAsync(); } } }

        private string _selectedLotNo;
        public string SelectedLotNo { get => _selectedLotNo; set { _selectedLotNo = value; OnPropertyChanged(nameof(SelectedLotNo)); } }

        private string _selectedParameter;
        public string SelectedParameter { get => _selectedParameter; set { _selectedParameter = value; OnPropertyChanged(nameof(SelectedParameter)); } }

        private string _selectedOperator;
        public string SelectedOperator { get => _selectedOperator; set { _selectedOperator = value; OnPropertyChanged(nameof(SelectedOperator)); } }

        private DateTime? _selectedDateTimeFrom = DateTime.Now.AddDays(-7);
        public DateTime? SelectedDateTimeFrom { get => _selectedDateTimeFrom; set { if (_selectedDateTimeFrom != value) { _selectedDateTimeFrom = value; OnPropertyChanged(nameof(SelectedDateTimeFrom)); _ = ReloadLotAsync(); } } }

        private DateTime? _selectedDateTimeTo = DateTime.Now;
        public DateTime? SelectedDateTimeTo { get => _selectedDateTimeTo; set { if (_selectedDateTimeTo != value) { _selectedDateTimeTo = value; OnPropertyChanged(nameof(SelectedDateTimeTo)); _ = ReloadLotAsync(); } } }

        // parameter display -> DTO property name
        private readonly Dictionary<string, string> ParameterToColumn = new()
        {
            ["Overall Length"] = "OL",
            ["Datum to End"] = "DE",
            ["Head Diameter"] = "HD",
            ["Groove Position"] = "GP",
            ["Stem Dia Near Groove"] = "STDG",
            ["Stem Dia Near Undercut"] = "STDU",
            ["Groove Diameter"] = "GIR_DIA",
            ["Straightness"] = "STN",
            ["Ovality SDG"] = "Ovality_SDG",
            ["Ovality SDU"] = "Ovality_SDU",
            ["Ovality Head"] = "Ovality_Head",
            ["Stem Taper"] = "Stem_Taper",
            ["End Face Runout"] = "EFRO",
            ["Face Runout"] = "Face_Runout",
            ["Seat Height"] = "SH",
            ["Seat Runout"] = "S_RO",
           ["Datum to Groove"] = "DG"

        };

        // Commands
        public ICommand SubmitCommand { get; }
        public ICommand CloseCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public RnR_Report_Page()
        {
            InitializeComponent();

            _dataService = new DataStorageService();

            SubmitCommand = new RelayCommand(async _ => await LoadRnRDataAsync(), _ => CanSubmit());
            CloseCommand = new RelayCommand(_ => ClosePage());

            LoadActiveParts();
            LoadOperators();

            DataContext = this;
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #region Load Filters

        private void LoadActiveParts()
        {
            try
            {
                ActiveParts.Clear();
                ActiveParts.Add("All");
                var parts = _dataService.GetActiveParts();
                if (parts != null)
                {
                    foreach (var p in parts)
                    {
                        if (!string.IsNullOrWhiteSpace(p.Para_No))
                            ActiveParts.Add(p.Para_No);
                    }
                }
                SelectedPartNo = ActiveParts.FirstOrDefault() ?? "All";
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
                    {
                        if (!string.IsNullOrWhiteSpace(lot) && !LotNumbers.Contains(lot))
                            LotNumbers.Add(lot);
                    }
                }
                SelectedLotNo = LotNumbers.FirstOrDefault() ?? "All";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadLotNumbersAsync] {ex.Message}");
            }
        }

        private void LoadParameters(string part)
        {
            try
            {
                ParametersOptions.Clear();
                var config = _dataService.GetPartConfig(part == "All" ? null : part);
                if (config != null)
                {
                    foreach (var c in config)
                        ParametersOptions.Add(c.Parameter);
                }
                SelectedParameter = ParametersOptions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadParameters] {ex.Message}");
            }
        }

        private void LoadOperators()
        {
            Operators.Clear();
            Operators.Add("All");
            SelectedOperator = "All";
        }

        private async Task LoadOperatorsAsync(string partNo)
        {
            try
            {
                var ops = await _dataService.GetOperatorsByPartAndDateRangeAsync(partNo == "All" ? null : partNo, SelectedDateTimeFrom, SelectedDateTimeTo);
                Operators.Clear();
                Operators.Add("All");
                if (ops != null)
                {
                    foreach (var op in ops)
                        if (!string.IsNullOrWhiteSpace(op) && !Operators.Contains(op))
                            Operators.Add(op);
                }
                SelectedOperator = Operators.FirstOrDefault() ?? "All";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadOperatorsAsync] {ex.Message}");
            }
        }

        private async Task ReloadLotAsync()
        {
            await LoadLotNumbersAsync(SelectedPartNo);
        }

        #endregion

        private bool CanSubmit()
        {
            return SelectedDateTimeFrom != null &&
                   SelectedDateTimeTo != null &&
                   !string.IsNullOrWhiteSpace(SelectedParameter);
        }

        private void ClosePage()
        {
            var window = Window.GetWindow(this);
            // optional: window?.Close();
        }

        // Main entry: load measurements and compute R&R
        private async Task LoadRnRDataAsync()
        {
            try
            {
                ClearAllGrids();
                ResetSummary();

                if (string.IsNullOrWhiteSpace(SelectedParameter))
                {
                    MessageBox.Show("Please select a parameter.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string part = SelectedPartNo == "All" ? null : SelectedPartNo;
                string lot = SelectedLotNo == "All" ? null : SelectedLotNo;
                string oper = SelectedOperator == "All" ? null : SelectedOperator;

                //-------------------------------------------------------
                // 1) GET PART CONFIG (Nominal, RTolPlus, RTolMinus)
                //-------------------------------------------------------
                var config = _dataService.GetPartConfig(part)
                                         ?.FirstOrDefault(c => c.Parameter == SelectedParameter);

                if (config == null)
                {
                    MessageBox.Show("Part configuration not found for selected parameter.",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                //-------------------------------------------------------
                // 2) GET COLUMN NAME FOR THIS PARAMETER
                //-------------------------------------------------------
                string col = null;
                if (ParameterToColumn.ContainsKey(SelectedParameter))
                    col = ParameterToColumn[SelectedParameter];
                else
                    col = SelectedParameter;    // FINAL fallback

                //-------------------------------------------------------
                // 3) FETCH MEASUREMENTS
                //-------------------------------------------------------
                var list = await _dataService.GetMeasurementReadingsAsync
                                (part, lot, oper, SelectedDateTimeFrom, SelectedDateTimeTo);

                if (list == null || !list.Any())
                {
                    MessageBox.Show("No measurement data found for the selected filters.",
                                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                //-------------------------------------------------------
                // 4) EXTRACT VALUES FOR SELECTED PARAMETER
                //-------------------------------------------------------
                List<double> allValues = new();

                foreach (var m in list)
                {
                    var prop = m.GetType().GetProperty(col);
                    if (prop != null && double.TryParse(prop.GetValue(m)?.ToString(), out double v))
                        allValues.Add(v);
                }

                if (allValues.Count < 90)
                {
                    MessageBox.Show($"Insufficient measurements: {allValues.Count} found. 90 required.",
                                    "Insufficient Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Only use first 90 readings
                allValues = allValues.Take(90).ToList();

                //-------------------------------------------------------
                // 5) SPLIT INTO 3 APPRAISERS (3 trials × 10 parts)
                //-------------------------------------------------------
                var a1 = allValues.Skip(0).Take(30).ToList();
                var a2 = allValues.Skip(30).Take(30).ToList();
                var a3 = allValues.Skip(60).Take(30).ToList();

                BuildGridForAppraiser(Appraiser1Data, a1);
                BuildGridForAppraiser(Appraiser2Data, a2);
                BuildGridForAppraiser(Appraiser3Data, a3);

                //-------------------------------------------------------
                // 6) TOLERANCE CALCULATION (✔ Correct for your model)
                //-------------------------------------------------------
                double nominal = config.Nominal;
                double rtPlus = config.RTolPlus;
                double rtMinus = config.RTolMinus;

                // LSL, USL, and final tolerance
                double LSL = nominal - rtMinus;
                double USL = nominal + rtPlus;

                // FINAL TOLERANCE USED IN GAUGE R&R
                double tolerance = USL - LSL;   // = RTolPlus + RTolMinus

                TotalTolerance = tolerance;

                //-------------------------------------------------------
                // 7) RUN GAUGE R&R CALCULATION
                //-------------------------------------------------------
                ComputeGaugeRrFromAppraisers(a1, a2, a3, tolerance);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error loading R&R", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // Build grid for a single appraiser: 3 rows x 10 columns + Average + Range
        private void BuildGridForAppraiser(ObservableCollection<RnRGridRow> target, List<double> values)
        {
            target.Clear();
            if (values == null || values.Count != 30) return;

            for (int r = 0; r < 3; r++)
            {
                int offset = r * 10;
                var row = new RnRGridRow
                {
                    TrialNo = (r + 1).ToString(),
                    Data1 = values[offset + 0],
                    Data2 = values[offset + 1],
                    Data3 = values[offset + 2],
                    Data4 = values[offset + 3],
                    Data5 = values[offset + 4],
                    Data6 = values[offset + 5],
                    Data7 = values[offset + 6],
                    Data8 = values[offset + 7],
                    Data9 = values[offset + 8],
                    Data10 = values[offset + 9]
                };
                target.Add(row);
            }

            // Average row
            var avgRow = new RnRGridRow { TrialNo = "Average" };
            var rangeRow = new RnRGridRow { TrialNo = "Range" };

            for (int c = 0; c < 10; c++)
            {
                double[] colvals = { target[0].GetColumn(c), target[1].GetColumn(c), target[2].GetColumn(c) };
                avgRow.SetColumn(c, colvals.Average());
                rangeRow.SetColumn(c, colvals.Max() - colvals.Min());
            }

            target.Add(avgRow);
            target.Add(rangeRow);
        }

        // Core calculation: takes appraisers raw 30-values lists
        private void ComputeGaugeRrFromAppraisers(List<double> a1, List<double> a2, List<double> a3, double tolerance)
        {
            // Create matrices 3 trials x 10 parts for each appraiser (same as before)
            double[,] m1 = ConvertToMatrix(a1);
            double[,] m2 = ConvertToMatrix(a2);
            double[,] m3 = ConvertToMatrix(a3);

            // Calculate per-appraiser averages (X1Bar, X2Bar, X3Bar) and Rbars (R1Bar, R2Bar, R3Bar)
            double X1Bar = 0, R1Bar = 0, X2Bar = 0, R2Bar = 0, X3Bar = 0, R3Bar = 0;

            for (int p = 0; p < 10; p++)
            {
                // Appraiser 1
                double[] vals1 = { m1[0, p], m1[1, p], m1[2, p] };
                X1Bar += vals1.Average();
                R1Bar += vals1.Max() - vals1.Min();

                // Appraiser 2
                double[] vals2 = { m2[0, p], m2[1, p], m2[2, p] };
                X2Bar += vals2.Average();
                R2Bar += vals2.Max() - vals2.Min();

                // Appraiser 3
                double[] vals3 = { m3[0, p], m3[1, p], m3[2, p] };
                X3Bar += vals3.Average();
                R3Bar += vals3.Max() - vals3.Min();
            }

            X1Bar /= 10; X2Bar /= 10; X3Bar /= 10;
            R1Bar /= 10; R2Bar /= 10; R3Bar /= 10;

            // Grand averages
            double XDoubleBar = (X1Bar + X2Bar + X3Bar) / 3;
            double RDoubleBar = (R1Bar + R2Bar + R3Bar) / 3;

            // VB6 AIAG Constants: k1=0.5908 (for 3 trials), k2=0.5231 (for 3 appraisers), k3=0.31 (for 10 parts)
            const double k1 = 0.5908;
            const double k2 = 0.5231;
            const double k3 = 0.31;
            const double ndcFactor = 1.41;

            // Equipment Variation (EV) - Repeatability
            double EV = RDoubleBar * k1;

            // Appraiser Variation (AV) - Reproducibility (VB6 method)
            double MaxXbar = Math.Max(X1Bar, Math.Max(X2Bar, X3Bar));
            double MinXbar = Math.Min(X1Bar, Math.Min(X2Bar, X3Bar));
            double L29 = MaxXbar - MinXbar;

            double B54 = (EV * EV) / 30.0;  // 30 = 10 parts * 3 trials
            double B53 = (L29 * k2) * (L29 * k2);

            double AV = Math.Sqrt(Math.Abs(B53 - B54));

            // Total GR&R (√(EV² + AV²))
            double GRR = Math.Sqrt(EV * EV + AV * AV);

            // Part Variation using Range method (Rp * k3)
            double[] partAverages = new double[10];
            for (int p = 0; p < 10; p++)
            {
                double[] partVals = { m1[0, p], m1[1, p], m1[2, p], m2[0, p], m2[1, p], m2[2, p], m3[0, p], m3[1, p], m3[2, p] };
                partAverages[p] = partVals.Average();
            }
            Array.Sort(partAverages);
            double Rp = partAverages[9] - partAverages[0];
            double PV = Rp * k3;

            // Total Variation
            double TV = Math.Sqrt(GRR * GRR + PV * PV);

            // %GRR of PV Method
            double percentGRR_PV = (TV > 0) ? (GRR / TV) * 100.0 : 0.0;

            // %PV Contribution
            double percentPV = (TV > 0) ? (PV / TV) * 100.0 : 0.0;

            // NDC (PV Method)
            double ndc_PV = (GRR > 0) ? ndcFactor * (PV / GRR) : 0;

            // %GRR of Tolerance Method
            double percentGRR_Tolerance = (tolerance > 0) ? (GRR / tolerance) * 100.0 : 0.0;

            // Conclusions (standard AIAG criteria)
            string conclusion_PV = percentGRR_PV <= 10 ? "Accepted" :
                                  percentGRR_PV <= 30 ? "Conditionally Accepted" : "Not Accepted";
            string conclusion_TT = percentGRR_Tolerance <= 10 ? "Accepted" :
                                  percentGRR_Tolerance <= 30 ? "Conditionally Accepted" : "Not Accepted";

            // Assign to properties (update your existing properties)
            Repeatability = EV;
            Reproducibility = AV;
            PartVariation = PV;
            TotalPVPercent = percentPV;
            TotalTolerance = tolerance;
            GRRPercentage = percentGRR_Tolerance;  // Use tolerance method as primary
            GRRConclusion = conclusion_TT;
            Ndc = (int)Math.Round(ndc_PV);

            // Debug output (optional - remove in production)
            System.Diagnostics.Debug.WriteLine($"VB6 Method: EV={EV:F4}, AV={AV:F4}, PV={PV:F4}, GRR={GRR:F4}");
            System.Diagnostics.Debug.WriteLine($"RDoubleBar={RDoubleBar:F5}, X1Bar={X1Bar:F4}, X2Bar={X2Bar:F4}");
        }

        // Converts a 30-element list into 3x10 matrix [trialIndex, partIndex]
        private double[,] ConvertToMatrix(List<double> list30)
        {
            var m = new double[3, 10];
            for (int t = 0; t < 3; t++)
                for (int p = 0; p < 10; p++)
                    m[t, p] = list30[t * 10 + p];
            return m;
        }

        private void ClearAllGrids()
        {
            Appraiser1Data.Clear();
            Appraiser2Data.Clear();
            Appraiser3Data.Clear();
        }

        private void ResetSummary()
        {
            Repeatability = 0;
            Reproducibility = 0;
            PartVariation = 0;
            TotalPVPercent = 0;
            TotalTolerance = 0;
            GRRPercentage = 0;
            GRRConclusion = string.Empty;
            Ndc = 0;
        }

        #region Helpers (RelayCommand + Grid Row)
        public class RelayCommand : ICommand
        {
            private readonly Func<object, Task> _executeAsync;
            private readonly Action<object> _execute;
            private readonly Predicate<object> _canExecute;

            public RelayCommand(Func<object, Task> executeAsync, Predicate<object> canExecute = null)
            {
                _executeAsync = executeAsync;
                _canExecute = canExecute;
            }

            public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

            public async void Execute(object parameter)
            {
                if (_executeAsync != null) await _executeAsync(parameter);
                else _execute?.Invoke(parameter);
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
        }

        public class RnRGridRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private string _trialNo;
            public string TrialNo { get => _trialNo; set { _trialNo = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrialNo))); } }

            private double _d1, _d2, _d3, _d4, _d5, _d6, _d7, _d8, _d9, _d10;
            public double Data1 { get => _d1; set { _d1 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data1))); } }
            public double Data2 { get => _d2; set { _d2 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data2))); } }
            public double Data3 { get => _d3; set { _d3 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data3))); } }
            public double Data4 { get => _d4; set { _d4 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data4))); } }
            public double Data5 { get => _d5; set { _d5 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data5))); } }
            public double Data6 { get => _d6; set { _d6 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data6))); } }
            public double Data7 { get => _d7; set { _d7 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data7))); } }
            public double Data8 { get => _d8; set { _d8 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data8))); } }
            public double Data9 { get => _d9; set { _d9 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data9))); } }
            public double Data10 { get => _d10; set { _d10 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data10))); } }

            public double GetColumn(int idx) => idx switch
            {
                0 => Data1,
                1 => Data2,
                2 => Data3,
                3 => Data4,
                4 => Data5,
                5 => Data6,
                6 => Data7,
                7 => Data8,
                8 => Data9,
                9 => Data10,
                _ => 0
            };

            public void SetColumn(int idx, double value)
            {
                switch (idx)
                {
                    case 0: Data1 = value; break;
                    case 1: Data2 = value; break;
                    case 2: Data3 = value; break;
                    case 3: Data4 = value; break;
                    case 4: Data5 = value; break;
                    case 5: Data6 = value; break;
                    case 6: Data7 = value; break;
                    case 7: Data8 = value; break;
                    case 8: Data9 = value; break;
                    case 9: Data10 = value; break;
                }
            }
        }
        #endregion
    }
}
