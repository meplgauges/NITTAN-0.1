using ActUtlTypeLib;
using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.Storage.Streams;
using Windows.UI.WindowManagement;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace EVMS.Service
{
    public class ProbeMeasurement
    {
        public string? ProbeId { get; set; }
        public string? Name { get; set; }
        public List<double> Readings { get; set; } = new List<double>();
        public double ReferenceValue { get; set; } = 0;
        public double MinReferenceValue { get; set; } = 0;  // Add this property
        public double MasterValue { get; set; } = 0;
        public double TolerancePlus { get; set; }
        public double ToleranceMinus { get; set; }
    }


    public class ParameterResult
    {
        public double Value { get; set; }
        public bool IsOk { get; set; }
    }
 

    public class MasterService : IDisposable
    {
        public event Action? MeasurementStarted;
        public event Action? MeasurementStopped;

        public bool _continueMeasurement = false;

        public bool IsMeasurementRunning { get; set; } = false;



        public event Action<string>? StatusMessageUpdated;
        private List<ProbeMeasurement> _orderedProbeMeasurements = new List<ProbeMeasurement>();

        public delegate void CalculatedValuesWithStatusHandler(object? sender, Dictionary<string, ParameterResult> results);
        public event CalculatedValuesWithStatusHandler? CalculatedValuesWithStatusReady;
        public event EventHandler<MasterCompletedEventArgs>? MasterCompleted;
        public delegate void CalculatedValuesReadyHandler(object? sender, Dictionary<string, double> calculatedValues);
        public event CalculatedValuesReadyHandler? CalculatedValuesReady;
        private bool _isMeasurementRunning = true;


        private readonly ActUtlType plc;
        private readonly DataStorageService _dataStorageService;
        private readonly PlcProbeService _plcProbeService;

        private Dictionary<string, ProbeMeasurement> _probeMeasurements = new Dictionary<string, ProbeMeasurement>();
        private const int ArraySize = 150;

        private readonly ConcurrentQueue<(string ProbeId, double Value)> _collectedReadings
            = new ConcurrentQueue<(string, double)>();

        private int _currentOperationalMode = 1;
        private string _currentPartCode = "";

        public bool IsMasteringStage { get; set; } = true;
        public bool MasterComplete { get; set; } = false;
        public bool MasteringOK { get; set; } = false;
        public bool Abort { get; set; } = false;
        public double MinReferenceValue { get; set; } = 0;


        //private const string MotorOnDevice = "M14";
        //private const string Auto  = "X14";

        public MasterService()
        {
            //var resultPage = new ResultPage();

            _dataStorageService = new DataStorageService();
            _plcProbeService = new PlcProbeService();
            plc = new ActUtlType { ActLogicalStationNumber = 1 };
            _plcProbeService.ProbeValueUpdated += ProbeValueUpdatedHandler;

        }
        public async Task StartMeasurementAsync()
        {
            _continueMeasurement = true;
            await RunMeasurementCycleAsync();
        }

        public void StopMeasurement()
        {
            _continueMeasurement = false;
        }

        protected virtual void OnCalculatedValuesWithStatusReady(Dictionary<string, ParameterResult> results)
        {
            CalculatedValuesWithStatusReady?.Invoke(this, results);
        }
        public bool IsConnected => _plcProbeService?.IsConnected ?? false;

        public async Task<bool> EnsureConnectionAsync()
        {
            if (_plcProbeService == null) return false;

            if (!_plcProbeService.IsConnected)
            {
                bool connected = await _plcProbeService.ConnectAsync();
                if (connected)
                {
                    _plcProbeService.ProbeValueUpdated += ProbeValueUpdatedHandler;
                    int openResult = plc.Open();
                    if (openResult != 0)
                    {
                        MessageBox.Show($"Failed to open PLC connection. Error code: {openResult}", "PLC Init Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
                return connected;
            }
            return true;
        }

        private void ProbeValueUpdatedHandler(object? sender, ProbeReadingEventArgs e)
        {
            _collectedReadings.Enqueue((e.ModuleId, e.Value));
        }

        public bool SetPlcDevice(string device, int value)
        {
            int ret = plc.SetDevice(device, (short)value);
            if (ret != 0)
            {
                NotifyStatus("SetDevice failed!!");

                return false;
            }
            return true;
        }

        public int GetPlcDeviceBit(string device)
        {
            if (plc.GetDevice(device, out int value) != 0)
            {
                NotifyStatus($"GetDevice failed for bit {device}! Exception");
                return -1; // Indicate failure
            }
            return value; // Return the bit value read from PLC device
        }


        private void Cleanup()
        {
            _plcProbeService.StopLiveReading();
            _plcProbeService?.Disconnect();
            _plcProbeService.ProbeValueUpdated -= ProbeValueUpdatedHandler;
            plc.Close();
        }

        public enum ProcedureMode
        {
            Mastering,
            MasterInspection,
            Measurement
        }

        public async Task MasterCheckProcedureAsync(ProcedureMode mode)
        {
            try
            {
                var autoList = _dataStorageService.GetActiveBit();
                if (autoList == null || autoList.Count == 0)
                {
                    MessageBox.Show("No Settings Found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var autoControl = autoList.FirstOrDefault(c => string.Equals(c.Description, "Auto/Manual", StringComparison.OrdinalIgnoreCase));
                int bitValue = GetPlcDeviceBit("X14");

                if ((autoControl?.Bit == 1 && bitValue == 1) || (autoControl?.Bit == 0 && bitValue == 0))
                {
                    // Modes matched: proceed with the rest of the procedure
                }
                else
                {
                    if (autoControl?.Bit == 0 && bitValue == 1)
                    {
                        MessageBox.Show("Software is in Manual mode. Please switch PLC to Manual mode.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (autoControl?.Bit == 1 && bitValue == 0)
                    {
                        MessageBox.Show("Software is in Auto mode. Change PLC to Auto mode.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;  // Exit early if modes do not match
                }


                var activeParts = _dataStorageService.GetActiveParts();
                if (activeParts == null || activeParts.Count == 0)
                {
                    MessageBox.Show("No active parts found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _currentPartCode = activeParts[0]?.Para_No ?? "";
                if (string.IsNullOrEmpty(_currentPartCode))
                {
                    MessageBox.Show("Active part code is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (autoControl?.Bit == 1)
                {
                    SetPlcDevice("M400", 1);
                    SetPlcDevice("M100", 0);
                    SetPlcDevice("M300", 0);
                    switch (mode)
                    {
                        case ProcedureMode.Mastering:
                            SetPlcDevice("M100", 1);
                            break;
                        case ProcedureMode.MasterInspection:
                            SetPlcDevice("M100", 1);
                            break;
                        case ProcedureMode.Measurement:
                            SetPlcDevice("M300", 1);
                            break;
                    }
                }

                SetPlcDevice("M10", 0);
                LoadProbeConfigurations(_currentPartCode);
                var sortedProbeMeasurements = _orderedProbeMeasurements;

                foreach (var pm in sortedProbeMeasurements)
                {
                    pm.Readings.Clear();
                    pm.ReferenceValue = 0;
                    pm.MinReferenceValue = 0;
                }

                if (mode == ProcedureMode.Measurement)
                {
                    // Start the measurement cycle asynchronously her
                    // Optionally handle any post-measurement processing her
                    return;
                }


                bool firstMeasurementCycle = true;
                if (mode == ProcedureMode.Mastering || mode == ProcedureMode.MasterInspection)
                {
                    if (autoControl?.Bit == 0)
                    {
                        string loadMsg = mode == ProcedureMode.Mastering ? "Load The Value in fixture..." : "Place part for measurement...";
                        await NotifyOnUIAsync(loadMsg);
                        await WaitForValidProbeReadingAsync("100AY08P42");
                        string promptMsg = mode == ProcedureMode.Mastering ? "PRESS START SWITCH TO START MASTERING" : "PRESS START SWITCH TO START MasterInspection";
                        await NotifyOnUIAsync(promptMsg);
                        while (GetPlcDeviceBit("X1") != 1) await Task.Delay(100);
                        await NotifyOnUIAsync("START BUTTON PRESSED");
                    }
                    else
                    {
                        
                        string startMsg = mode == ProcedureMode.Mastering ? "Press Robo start button to start mastering." : "Press Robo start button to start MasterInspection.";
                        await NotifyOnUIAsync(startMsg);
                        while (GetPlcDeviceBit("M20") != 1) await Task.Delay(100);
                        await NotifyOnUIAsync("Robo start button Pressed");//DIRECTLY GETTING THE ROBO START
                        SetPlcDevice("M101", 1);
                        await NotifyOnUIAsync("Waiting Robot to Load Part...");
                        await WaitForValidProbeReadingAsync("100AY08P42");
                        await NotifyOnUIAsync("Waiting Robot for Safe Position...");
                        while (GetPlcDeviceBit("M101") != 0) await Task.Delay(100);
                    }
                    
                        await RunMotorAndCollectReadingsAsync(sortedProbeMeasurements, mode);
                    
                    await HandleProcedurePostProcessingAsync(mode, sortedProbeMeasurements, firstMeasurementCycle);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in MasterCheckProcedure: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ** Separate measurement function for external toggle to run cycle **
        public async Task RunMeasurementCycleAsync()
        {
            var autoList = _dataStorageService.GetActiveBit();
            var autoControl = autoList?.FirstOrDefault(c =>
                string.Equals(c.Description, "Auto/Manual", StringComparison.OrdinalIgnoreCase));
            int bitValue = GetPlcDeviceBit("X14"); // Auto/Manual PLC bit

            if (!(autoControl != null && (autoControl.Bit == bitValue)))
            {
                if (autoControl?.Bit == 0 && bitValue == 1)
                    await NotifyOnUIAsync("Software is in Manual mode. Please switch PLC to Manual mode.");
                else if (autoControl?.Bit == 1 && bitValue == 0)
                    await NotifyOnUIAsync("Software is in Auto mode. Change PLC to Auto mode.");
                return;
            }

            var sortedProbeMeasurements = _orderedProbeMeasurements;
            bool firstMeasurementCycle = true;

            do
            {
                // Only check _continueMeasurement before starting a new cycle
                if (!firstMeasurementCycle && !_continueMeasurement)
                {
                    await NotifyOnUIAsync("Stop requested. Finishing current cycle before stopping...");
                    break; // exit loop after finishing current cycle
                }

                // Clear previous readings if not the first cycle
                if (!firstMeasurementCycle)
                {
                    foreach (var probe in sortedProbeMeasurements)
                        probe.Readings.Clear();
                }

                // Wait for robot safe position if not the first cycle
                if (!firstMeasurementCycle)
                {
                    await NotifyOnUIAsync("Waiting for robot to reach safe position...");
                    while (GetPlcDeviceBit("M301") != 0) await Task.Delay(100);
                    await NotifyOnUIAsync("Robot is in safe position. Ready to load next part.");
                }

                // ===== Start Cycle =====
                if (firstMeasurementCycle)
                {
                    if (autoControl?.Bit == 0)
                    {
                        await NotifyOnUIAsync("Place part for measurement...");
                        await WaitForValidProbeReadingAsync("100AY08P42");

                        await NotifyOnUIAsync("PRESS START SWITCH TO START MEASUREMENT");
                        while (GetPlcDeviceBit("X1") != 1) await Task.Delay(100);

                        await NotifyOnUIAsync("START BUTTON PRESSED");
                    }
                    else
                    {
                        if (!_continueMeasurement) break;

                        await NotifyOnUIAsync("Press Robo start button to start measurement.");
                        while (GetPlcDeviceBit("M20") != 1 && _continueMeasurement) await Task.Delay(100);

                        if (!_continueMeasurement) break;

                        await NotifyOnUIAsync("Robo start button Pressed");
                        SetPlcDevice("M301", 1);

                        await NotifyOnUIAsync("Waiting Robot to Load Part...");
                        if (_continueMeasurement)
                            await WaitForValidProbeReadingAsync("100AY08P42");

                        if (!_continueMeasurement) break;

                        await NotifyOnUIAsync("Waiting Robot for Safe Position...");
                        while (GetPlcDeviceBit("M301") != 0 && _continueMeasurement) await Task.Delay(100);
                    }
                }
                else
                {
                    if (!_continueMeasurement) break;

                    await NotifyOnUIAsync("Robo start button Pressed");
                    SetPlcDevice("M301", 1);

                    await NotifyOnUIAsync("Waiting Robot to Load Part...");
                    if (_continueMeasurement)
                        await WaitForValidProbeReadingAsync("100AY08P42");

                    if (!_continueMeasurement) break;

                    await NotifyOnUIAsync("Waiting Robot for Safe Position...");
                    while (GetPlcDeviceBit("M301") != 0 && _continueMeasurement) await Task.Delay(100);
                }

                /// Only run motor if measurement is ON
                if (_continueMeasurement)
                {
                    await RunMotorAndCollectReadingsAsync(sortedProbeMeasurements, ProcedureMode.Measurement);
                }


                var probeMeasurementByNameMeasurement = sortedProbeMeasurements
                    .Where(pm => !string.IsNullOrEmpty(pm.Name))
                    .ToDictionary(pm => pm.Name!);

                HandleMeasurementStage(probeMeasurementByNameMeasurement, _currentPartCode);

                var readingsSummary = string.Join("; ",
                    sortedProbeMeasurements.Select(p =>
                        $"{p.Name}: {string.Join(",", p.Readings)}"));

                await NotifyOnUIAsync($"Cycle completed. Probe readings: {readingsSummary}");

                firstMeasurementCycle = false;

                await NotifyOnUIAsync("Ready for next cycle.");

            } while (ShouldContinueMeasurement());

            // ===== Stop & Reset =====
            await NotifyOnUIAsync("Measurement cycle stopped by user.");

            SetPlcDevice("M10", 0);   // Motor OFF
                                      //SetPlcDevice("M20", 0);   // Robo Start OFF if needed
            SetPlcDevice("M300", 0);  // Measurement Complete OFF
                                      //SetPlcDevice("M301", 0);  // Robot Safe Position Reset if needed

            MeasurementStopped?.Invoke();
        }

        // Helper should continue method





        private async Task RunMotorAndCollectReadingsAsync(List<ProbeMeasurement> sortedProbeMeasurements, ProcedureMode mode)
        {
            
            
            SetPlcDevice("M10", 1);
            NotifyStatus("Collecting the readings");

            // Wait for PLC ready signal
            while (GetPlcDeviceBit("X0") != 1)
                await Task.Delay(100);

            SetPlcDevice("M14", 1);

            // Clear old data once
            while (_collectedReadings.TryDequeue(out _)) { }

            // Start live reading once
            _plcProbeService.StartLiveReading();

            for (int i = 0; i < ArraySize; i++)
            {
                if (Abort)
                {
                    MessageBox.Show("Operation aborted.", "Abort", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                }

                await Task.Delay(15);

                while (_collectedReadings.TryDequeue(out var reading))
                {
                    if (_probeMeasurements.TryGetValue(reading.ProbeId, out var pm))
                    {
                        if (pm.Readings.Count < ArraySize)
                        {
                            pm.Readings.Add(reading.Value);
                        }
                    }
                }

                if (_probeMeasurements.Values.All(pm => pm.Readings.Count >= ArraySize))
                {
                    // All probes have enough readings, stop the loop
                    break;
                }
            }

            // Stop reading
            _plcProbeService.StopLiveReading();
            SetPlcDevice("M14", 0);
            SetPlcDevice("M10", 0);

            // Process readings for each probe
            foreach (var pm in sortedProbeMeasurements)
            {
                // Clean up readings
                var validReadings = pm.Readings
                    .Where(r => !double.IsNaN(r))
                    .OrderBy(r => r) // sort values (ascending)
                    .ToList();

                if (validReadings.Count > 0)
                {
                    pm.ReferenceValue = validReadings.Max();
                    pm.MinReferenceValue = validReadings.Min(); // always calculate min
                }
                else
                {
                    pm.ReferenceValue = 0;
                    pm.MinReferenceValue = 0;
                }

            }

            NotifyStatus("Reading collection completed.");
        }

        private async Task HandleProcedurePostProcessingAsync(ProcedureMode mode, List<ProbeMeasurement> probeMeasurements, bool isFirstMeasurementCycle)
        {
            switch (mode)
            {
                case ProcedureMode.Mastering:
                    await NotifyOnUIAsync("Mastering Completed. Press Enter to Inspect the Master");

                    var masterValues = probeMeasurements
                        .Where(pm => !string.IsNullOrEmpty(pm.Name))
                        .ToDictionary(pm => pm.Name!, pm => pm.ReferenceValue);

                    OnCalculatedValuesReady(masterValues);

                    await HandleMasteringStageAsync();
                    break;

                case ProcedureMode.MasterInspection:
                    var probeMeasurementByName = probeMeasurements
                        .Where(pm => !string.IsNullOrEmpty(pm.Name))
                        .ToDictionary(pm => pm.Name!);

                    HandleMasterInspectionStage(probeMeasurementByName, _currentPartCode);

                    SetPlcDevice("M102", 1);

                    await NotifyOnUIAsync("Waiting Safe position from Robot");
                    while (GetPlcDeviceBit("M102") != 0)
                        await Task.Delay(100);

                    await NotifyOnUIAsync("Master Inspection Completed");
                    break;

                
            }
        }

        




        private async Task HandleMasteringStageAsync()
        {
            if (Abort)
            {
                MessageBox.Show("Mastering aborted.", "Abort", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var pm in _probeMeasurements.Values)
            {
                _dataStorageService.SaveProbeReadings(
                    _dataStorageService.GetProbeInstallByPartNumber(_currentPartCode),
                    _currentPartCode,
                    new Dictionary<string, double> { { pm.ProbeId, pm.ReferenceValue } }
                );
            }

            MasterComplete = true;
            var resultsWithStatus = _probeMeasurements.Values
                .Where(pm => !string.IsNullOrEmpty(pm.Name))
                .ToDictionary(
                    pm => pm.Name!,
                    pm => new ParameterResult { Value = pm.ReferenceValue, IsOk = true } // assuming OK on mastering
                );
            OnCalculatedValuesWithStatusReady(resultsWithStatus);

            // Determine current mode
            var autoList = _dataStorageService.GetActiveBit();
            var autoControl = autoList?.FirstOrDefault(c => string.Equals(c.Description, "Auto/Manual", StringComparison.OrdinalIgnoreCase));
            int bitValue = GetPlcDeviceBit("X14"); // PLC Auto/Manual bit

            bool isAuto = autoControl != null && autoControl.Bit == 1 && bitValue == 1;

            if (isAuto)
            {
                SetPlcDevice("M102", 1); // trigger robot to move to safe position
                await NotifyOnUIAsync("Waiting Safe position from Robot...");
                while (GetPlcDeviceBit("M102") != 0)
                    await Task.Delay(100);
            }

            // Mastering complete message
            await NotifyOnUIAsync("Mastering Completed. Press Enter to Inspect the Master");
        }


        private bool IsValidParameter(string param, Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            return probeMeasurements.ContainsKey(param) && dbRefDict.ContainsKey(param);
        }

        private void HandleMasterInspectionStage(Dictionary<string, ProbeMeasurement> probeMeasurements, string partCode)
        {
            var dbRefList = _dataStorageService.GetMasterProbeRef(_currentPartCode);
            var dbRefDict = dbRefList.ToDictionary(x => x.Name, x => x.Value);

            // Fetch master part configurations including tolerances
            var masterVals = _dataStorageService.GetPartConfig(partCode);

            string[] parameterNames = new string[]
            {
        "Overall Length", "Datum to End", "Head Diameter", "Groove Position",
        "Stem Dia Near Groove", "Stem Dia Near Undercut", "Groove Diameter",
        "Straightness", "Seat Height", "Seat Runout", "Datum to Groove",
        "Ovality SDG", "Ovality SDU", "Ovality Head", "Stem Taper",
        "Face Runout", "End Face Runout"
            };

            var calculatedValues = new Dictionary<string, double>();

            // Calculate values for each parameter
            foreach (var paramName in parameterNames)
            {
                try
                {
                    double val = CalculateProbeValue(paramName, probeMeasurements, dbRefDict);
                    calculatedValues[paramName] = val;
                }
                catch
                {
                    calculatedValues[paramName] = double.NaN;
                }
            }

            bool overallOk = true;
            var resultsWithStatus = new Dictionary<string, ParameterResult>();

            foreach (var paramName in parameterNames)
            {
                double val = calculatedValues.ContainsKey(paramName) ? calculatedValues[paramName] : double.NaN;

                // Find matching config entry for parameter name or probe id
                var config = masterVals?.FirstOrDefault(m =>
                    string.Equals(m.Para_No, paramName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.Parameter, paramName, StringComparison.OrdinalIgnoreCase)
                );

                double masterVal = config?.Nominal ?? 0;
                double tolPlus = config?.RTolPlus ?? 0;
                double tolMinus = config?.RTolMinus ?? 0;

                double minAllowed = masterVal - tolMinus;
                double maxAllowed = masterVal + tolPlus;

                bool isOk = !double.IsNaN(val) && val >= minAllowed && val <= maxAllowed;

                if (!isOk) overallOk = false;

                resultsWithStatus[paramName] = new ParameterResult
                {
                    Value = Math.Abs(val),
                    IsOk = isOk
                };
            }

            MasteringOK = overallOk;
            OnCalculatedValuesWithStatusReady(resultsWithStatus);

            // Optional Debug output
            System.Diagnostics.Debug.WriteLine("=== MASTER INSPECTION RESULTS WITH TOLERANCES ===");
            foreach (var kvp in resultsWithStatus)
            {
                System.Diagnostics.Debug.WriteLine($"{kvp.Key}: {kvp.Value.Value:F4} [{(kvp.Value.IsOk ? "OK" : "NG")}]");
            }
        }


        private void HandleMeasurementStage(Dictionary<string, ProbeMeasurement> probeMeasurements, string partCode)
        {
            // Get reference master values
            var dbRefList = _dataStorageService.GetMasterProbeRef(partCode);
            var dbRefDict = dbRefList.ToDictionary(x => x.Name, x => x.Value);

            // Load master part configurations (nominal values and tolerances)
            var masterVals = _dataStorageService.GetPartConfig(partCode);

            // 17 measurement parameters in order
            string[] parameterNames = new string[]
            {
        "Overall Length", "Datum to End", "Head Diameter", "Groove Position",
        "Stem Dia Near Groove", "Stem Dia Near Undercut", "Groove Diameter",
        "Straightness", "Seat Height", "Seat Runout", "Datum to Groove",
        "Ovality SDG", "Ovality SDU", "Ovality Head", "Stem Taper",
        "Face Runout", "End Face Runout"
            };

            var calculatedValues = new Dictionary<string, double>();

            // Calculate each probe value
            foreach (var paramName in parameterNames)
            {
                try
                {
                    calculatedValues[paramName] = CalculateProbeValue(paramName, probeMeasurements, dbRefDict);
                }
                catch
                {
                    calculatedValues[paramName] = double.NaN;
                }
            }

            // Determine overall OK/NG and prepare results
            bool overallOk = true;
            var resultsWithStatus = new Dictionary<string, ParameterResult>();

            foreach (var paramName in parameterNames)
            {
                double val = calculatedValues[paramName];

                var config = masterVals?.FirstOrDefault(m =>
                    string.Equals(m.Para_No, paramName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.Parameter, paramName, StringComparison.OrdinalIgnoreCase)
                );

                double masterVal = config?.Nominal ?? 0;
                double tolPlus = config?.RTolPlus ?? 0;
                double tolMinus = config?.RTolMinus ?? 0;

                double minAllowed = masterVal - tolMinus;
                double maxAllowed = masterVal + tolPlus;

                bool isOk = !double.IsNaN(val) && val >= minAllowed && val <= maxAllowed;
                if (!isOk) overallOk = false;

                resultsWithStatus[paramName] = new ParameterResult
                {
                    Value = Math.Abs(val),
                    IsOk = isOk
                };
            }

            MasteringOK = overallOk;

            // Send calculated results to the Result Page
            OnCalculatedValuesWithStatusReady(resultsWithStatus);



            try
            {
                // Count number of NG parameters
                int ngCount = resultsWithStatus.Count(r => !r.Value.IsOk);

                if(ngCount > 0)
                {
                    SetPlcDevice("M13", 1); // Turn ON
                    Task.Run(async () =>
                    {
                        await Task.Delay(1);
                        SetPlcDevice("M13", 0); // Turn OFF
                    });
                }
                // If more than 2 parameters failed, trigger general rejection
                if (ngCount > 2)
                {
                    SetPlcDevice("M302", 1); // General rejection
                }
                else if (resultsWithStatus.ContainsKey("Seat Height") && !resultsWithStatus["Seat Height"].IsOk)
                {
                    SetPlcDevice("M305", 1); // Seat Height rejection
                }
                else if (resultsWithStatus.ContainsKey("Seat Runout") && !resultsWithStatus["Seat Runout"].IsOk)
                {
                    SetPlcDevice("M303", 1); // Seat Runout rejection
                }
                else if ((resultsWithStatus.ContainsKey("Stem Dia Near Groove") && !resultsWithStatus["Stem Dia Near Groove"].IsOk) ||
                         (resultsWithStatus.ContainsKey("Stem Dia Near Undercut") && !resultsWithStatus["Stem Dia Near Undercut"].IsOk))
                {
                    SetPlcDevice("M304", 1); // Stem Dia rejection
                }
                else if ((resultsWithStatus.ContainsKey("Groove Diameter") && !resultsWithStatus["Groove Diameter"].IsOk) ||
                         (resultsWithStatus.ContainsKey("Groove Position") && !resultsWithStatus["Groove Position"].IsOk))
                {
                    SetPlcDevice("M307", 1); // Groove Dia / Groove Position rejection
                }
                else
                {
                    SetPlcDevice("M306", 1); // All OK
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting PLC bits for rejection: {ex.Message}");
            }

        }

        protected virtual void OnCalculatedValuesReady(Dictionary<string, double> calculatedValues)
        {
            CalculatedValuesReady?.Invoke(this, calculatedValues);
        }

        // Calculation dispatcher adapted to accept both probeMeasurements and dbRefDict
        private double CalculateProbeValue(string dbName, Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            // Normalize dbName to lower case for uniform comparison
            switch (dbName.ToLower())
            {
                case "datum to end": return CalculateDatumToEnd(probeMeasurements, dbRefDict);
                case "overall length": return CalculateOverallLength(probeMeasurements, dbRefDict);
                case "head diameter": return CalculateHeadDiameter(probeMeasurements, dbRefDict);
                case "groove position": return CalculateGroovePosition(probeMeasurements, dbRefDict);
                case "stem dia near groove": return CalculateStemDia1(probeMeasurements, dbRefDict);
                case "stem dia near undercut": return CalculateStemDia2(probeMeasurements, dbRefDict);
                case "groove diameter": return CalculateGrooveDia(probeMeasurements, dbRefDict);
                case "straightness": return CalculateReducedDia(probeMeasurements, dbRefDict);
                case "end face runout": return CalculateEFRO(probeMeasurements, dbRefDict);
                case "seat height": return CalculateSeatHeight(probeMeasurements, dbRefDict);
                case "seat runout": return CalculateSeatRunout(probeMeasurements, dbRefDict);
                case "datum to groove": return CalculateDatumToGroove(probeMeasurements, dbRefDict);
                case "ovality sdg": return CalculateOvalitySDG(probeMeasurements, dbRefDict);
                case "ovality sdu": return CalculateOvalitySDU(probeMeasurements, dbRefDict);
                case "ovality head": return CalculateOvalityHead(probeMeasurements, dbRefDict);
                case "stem taper": return CalculateStemTaper(probeMeasurements, dbRefDict);
                case "face runout": return CalculateFaceRunout(probeMeasurements, dbRefDict);

                default: throw new Exception($"Unknown probe DB name: {dbName}");
            }
        }

        #region Calculation Methods

        private double CalculateDatumToEnd(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Datum to End", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("Datum to End", out var dbRefValue)) dbRefValue = 0.0;

            double current = pm.ReferenceValue;
            double offset = current - dbRefValue;

            double datumToEnd = Math.Abs(offset);
            if (offset > 0)
                datumToEnd += datumToEnd * 0.33;
            else if (offset < 0)
                datumToEnd += datumToEnd * 0.40;

            return Math.Round(pm.MasterValue - datumToEnd, 4);
        }

        private double CalculateOverallLength(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Overall Length", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("Overall Length", out var dbRefValue)) dbRefValue = 0.0;

            double offset = pm.ReferenceValue - dbRefValue;
            return Math.Round(pm.MasterValue + offset, 3);
        }

        private double CalculateHeadDiameter(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Head Diameter", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("Head Diameter", out var dbRefValue)) dbRefValue = 0.0;

            double offset = pm.ReferenceValue - dbRefValue;
            return Math.Round(pm.MasterValue + offset, 3);
        }

        private double CalculateGroovePosition(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Groove Position", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("Groove Position", out var dbRefValue)) dbRefValue = 0.0;

            double offset = pm.ReferenceValue - dbRefValue;
            return Math.Round(pm.MasterValue + offset, 3);
        }

        private double CalculateStemDia1(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Stem Dia Near Groove", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("Stem Dia Near Groove", out var dbRefValue)) dbRefValue = 0.0;

            double offset = pm.ReferenceValue - dbRefValue;
            return Math.Round(pm.MasterValue + offset, 3);
        }

        private double CalculateStemDia2(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Stem Dia Near Undercut", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("Stem Dia Near Undercut", out var dbRefValue)) dbRefValue = 0.0;

            double offset = pm.ReferenceValue - dbRefValue;
            return Math.Round(pm.MasterValue + offset, 3);
        }

        private double CalculateReducedDia(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Straightness", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("Straightness", out var dbRefValue)) dbRefValue = 0.0;

            // For ReducedDia, you may need a second probe or different logic; simplified here:
            double offset = pm.ReferenceValue - dbRefValue;
            if (offset < 0.001)
                offset = 0.001;
            return Math.Round(pm.MasterValue + offset, 3);
        }

        private double CalculateEFRO(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("End Face Runout", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("End Face Runout", out var dbRefValue)) dbRefValue = 0.0;

            double offset = pm.ReferenceValue - dbRefValue;
            return Math.Round(pm.MasterValue + offset, 3);
        }

        private double CalculateGrooveDia(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Groove Diameter", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("Groove Diameter", out var dbRefValue)) dbRefValue = 0.0;

            double offset = pm.ReferenceValue - dbRefValue;
            return Math.Round(pm.MasterValue + offset, 3);
        }

        private double CalculateSeatHeight(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            double ol = CalculateOverallLength(probeMeasurements, dbRefDict);
            double de = CalculateDatumToEnd(probeMeasurements, dbRefDict);

            if (double.IsNaN(ol) || double.IsNaN(de)) return double.NaN;
            return Math.Round(ol - de, 3);
        }

        private double CalculateSeatRunout(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Datum to End", out var pm)) return double.NaN;

            double seatRunout = pm.ReferenceValue - pm.MinReferenceValue;
            return Math.Round(seatRunout, 3);
        }


        private double CalculateDatumToGroove(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            double de = CalculateDatumToEnd(probeMeasurements, dbRefDict);
            double gp = CalculateGroovePosition(probeMeasurements, dbRefDict);

            if (double.IsNaN(de) || double.IsNaN(gp)) return double.NaN;
            return Math.Round(de - gp, 3);
        }

        private double CalculateOvalitySDG(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            // Use "Stem Dia Near Groove" probe's min and max readings for Ovality SDG
            if (!probeMeasurements.TryGetValue("Stem Dia Near Groove", out var pm)) return double.NaN;

            double result = pm.ReferenceValue - pm.MinReferenceValue;
            return Math.Round(result, 3);
        }


        private double CalculateOvalitySDU(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Stem Dia Near Undercut", out var pm)) return double.NaN;

            double result = pm.ReferenceValue - pm.MinReferenceValue;

            if (result < .001)
                result = 0.001;

            return Math.Round(result, 3);
        }



        private double CalculateOvalityHead(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Head Diameter", out var pm)) return double.NaN;

            double ovality = pm.ReferenceValue - pm.MinReferenceValue;
            return Math.Round(ovality, 3);
        }


        private double CalculateStemTaper(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            double stemDia1 = CalculateStemDia1(probeMeasurements, dbRefDict);
            double stemDia2 = CalculateStemDia2(probeMeasurements, dbRefDict);

            if (double.IsNaN(stemDia1) || double.IsNaN(stemDia2))
                return double.NaN;

            double stemTaper = stemDia1 - stemDia2;
            return Math.Round(stemTaper, 3);
        }


        private double CalculateFaceRunout(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            if (!probeMeasurements.TryGetValue("Overall Length", out var pm)) return double.NaN;

            double faceRunout = pm.ReferenceValue - pm.MinReferenceValue;
            return Math.Round(faceRunout, 3);
        }



        #endregion


        private void LoadProbeConfigurations(string partCode)
        {
            _probeMeasurements.Clear();
            _orderedProbeMeasurements.Clear();

            var probeInstalls = _dataStorageService.GetProbeInstallByPartNumber(partCode);
            var masterVals = _dataStorageService.GetPartConfig(partCode); // <-- Updated here

            foreach (var probe in probeInstalls)
            {
                var config = masterVals?
                    .FirstOrDefault(m =>
                        string.Equals(m.Para_No, probe.ProbeId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m.Parameter, probe.Name, StringComparison.OrdinalIgnoreCase)
                    );

                double masterVal = config?.Nominal ?? 0;
                double tolPlus = config?.RTolPlus ?? 0;
                double tolMinus = config?.RTolMinus ?? 0;

                var pm = new ProbeMeasurement
                {
                    ProbeId = probe.ProbeId,
                    Name = probe.Name,
                    MasterValue = masterVal,
                    TolerancePlus = tolPlus,
                    ToleranceMinus = tolMinus
                };

                _probeMeasurements[probe.ProbeId] = pm;
                _orderedProbeMeasurements.Add(pm); // Keep ordered list in database order
            }

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Loaded {_probeMeasurements.Count} probe configs for part {partCode}");
            foreach (var pm in _orderedProbeMeasurements)
            {
                System.Diagnostics.Debug.WriteLine($"  Probe {pm.ProbeId} ({pm.Name}): Master={pm.MasterValue}, Tol±={pm.TolerancePlus}/{pm.ToleranceMinus}");
            }
        }



        public void Dispose()
        {
            Cleanup();
            _dataStorageService?.Dispose();
        }

        public class MasterCompletedEventArgs : EventArgs
        {
            public Dictionary<string, double> MasteredValues { get; }
            public bool Success { get; }

            public MasterCompletedEventArgs(Dictionary<string, double> values, bool success)
            {
                MasteredValues = values;
                Success = success;
            }
        }

        private void OnMasterCompleted(Dictionary<string, double> values, bool success)
        {
            MasterCompleted?.Invoke(this, new MasterCompletedEventArgs(values, success));
        }
        //        
        public async Task WaitForValidProbeReadingAsync(string targetProbeId, bool suppressDetectedMessage = false)
        {
            await NotifyOnUIAsync("Initializing probe readings...");

            _plcProbeService.StartLiveReading(100);

            bool partDetected = false;
            bool messageFired = false;

            if (!suppressDetectedMessage)
                if (GetPlcDeviceBit("X14") == 1)
                    await NotifyOnUIAsync("Waiting the robo to load part");
                else
                    await NotifyOnUIAsync("Load the part");
            while (!partDetected)
            {
                await Task.Delay(100);

                var probeVal = _collectedReadings
                    .Where(r => r.ProbeId == targetProbeId)
                    .Select(r => r.Value)
                    .LastOrDefault();

                bool partPresent = Math.Abs(probeVal) > 0.100;

                if (partPresent && !messageFired)
                {
                    messageFired = true;
                    partDetected = true;
                    if (!suppressDetectedMessage)
                        await NotifyOnUIAsync("Part detected. Proceeding...");
                }
                else if (!partPresent && messageFired)
                {
                    messageFired = false;
                    if (!suppressDetectedMessage)
                        await NotifyOnUIAsync("Part removed. Waiting for new part...");
                }
                else if (!partPresent && !messageFired)
                {
                    if (!suppressDetectedMessage)
                        if (GetPlcDeviceBit("X14") == 1)
                            await NotifyOnUIAsync("Waiting the robo to load part");
                        else
                            await NotifyOnUIAsync("Load the part");
                }
            }

            _plcProbeService.StopLiveReading();

            if (!suppressDetectedMessage)
                await NotifyOnUIAsync("Values updated...");
        }


        private void NotifyStatus(string message)
        {
            var handler = StatusMessageUpdated;
            handler?.Invoke(message);
        }



        private async Task NotifyOnUIAsync(string message)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
                NotifyStatus(message);
            else
                await Application.Current.Dispatcher.InvokeAsync(() => NotifyStatus(message));
        }




        private bool ShouldContinueMeasurement()
        {
            return _continueMeasurement;
        }



        // In MasterService class
        public void ResetRejectionBits()
        {
            SetPlcDevice("M302", 0); // General rejection
            SetPlcDevice("M303", 0); // SRO rejection
            SetPlcDevice("M304", 0); // STDIA rejection
            SetPlcDevice("M305", 0); // Seat Height rejection
            SetPlcDevice("M307", 0); // Groove Diameter/Position rejection
        }





    }

}
