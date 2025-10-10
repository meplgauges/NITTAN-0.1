using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ActUtlTypeLib;
using Windows.Storage.Streams;
using Windows.UI.WindowManagement;

namespace EVMS.Service
{
    public class ProbeMeasurement
    {
        public string? ProbeId { get; set; }
        public string? Name { get; set; }  // Add this property

        public List<double> Readings { get; set; } = new List<double>();
        public double ReferenceValue { get; set; } = 0;
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
        public event Action<string>? StatusMessageUpdated;
        private List<ProbeMeasurement> _orderedProbeMeasurements = new List<ProbeMeasurement>();

        public delegate void CalculatedValuesWithStatusHandler(object? sender, Dictionary<string, ParameterResult> results);
        public event CalculatedValuesWithStatusHandler? CalculatedValuesWithStatusReady;
        public event EventHandler<MasterCompletedEventArgs>? MasterCompleted;
        public delegate void CalculatedValuesReadyHandler(object? sender, Dictionary<string, double> calculatedValues);
        public event CalculatedValuesReadyHandler? CalculatedValuesReady;

        private readonly ActUtlType plc;
        private readonly DataStorageService _dataStorageService;
        private readonly PlcProbeService _plcProbeService;

        private Dictionary<string, ProbeMeasurement> _probeMeasurements = new Dictionary<string, ProbeMeasurement>();
        private const int ArraySize = 200;

        private readonly ConcurrentQueue<(string ProbeId, double Value)> _collectedReadings
            = new ConcurrentQueue<(string, double)>();

        private int _currentOperationalMode = 1;
        private string _currentPartCode = "";

        public bool IsMasteringStage { get; set; } = true;
        public bool MasterComplete { get; set; } = false;
        public bool MasteringOK { get; set; } = false;
        public bool Abort { get; set; } = false;

        //private const string MotorOnDevice = "M14";
        //private const string Auto  = "X14";

        public MasterService()
        {
            _dataStorageService = new DataStorageService();
            _plcProbeService = new PlcProbeService();
            plc = new ActUtlType { ActLogicalStationNumber = 1 };
            _plcProbeService.ProbeValueUpdated += ProbeValueUpdatedHandler;
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

        private bool SetPlcDevice(string device, int value)
        {
            int ret = plc.SetDevice(device, (short)value);
            if (ret != 0)
            {
                MessageBox.Show($"SetDevice failed: Device={device}, Code={ret}", "PLC Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private int GetPlcDeviceBit(string device)
        {
            if (plc.GetDevice(device, out int value) != 0)
            {
                MessageBox.Show($"GetDevice failed: Device={device}", "PLC Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public async Task MasterCheckProcedureAsync()
        {
            try
            {
                var autoList = _dataStorageService.GetActiveBit();

                if (autoList == null || autoList.Count == 0)
                {
                    MessageBox.Show("No Settings Found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var autoControl = autoList.FirstOrDefault(c =>
                    string.Equals(c.Description, "Auto/Manual", StringComparison.OrdinalIgnoreCase));

                int bitValue = GetPlcDeviceBit("X14");

                if (autoControl?.Bit == 1 && bitValue == 1)
                {
                    //MessageBox.Show("System is in Manula mode mode. Please switch to Auto mode.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    //return;
                    MessageBox.Show("PLC and Software Both found in Same Mode.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);

                }
                else if (autoControl?.Bit == 0 && bitValue == 0)
                {
                    //MessageBox.Show("System is in Manula mode mode. Please switch to Auto mode.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    //return;
                    MessageBox.Show("PLC and Software Both found in Same Mode.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);

                }
                else if (autoControl?.Bit == 0 && bitValue == 1)
                {
                    MessageBox.Show("Software is in Manual mod. Please switch PLC to Manual mode.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                else if (autoControl?.Bit == 1 && bitValue == 0)
                {
                    MessageBox.Show("Software is in Auto mode. Change PLC to Auto mode.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
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
                    SetPlcDevice("M400", 1); // vb ready plc 
                    SetPlcDevice("M300", 0); // Measurement Cycle off
                    SetPlcDevice("M100", 1); // Master Cycle selected
                }

                SetPlcDevice("M10", 0); // Auto Weight on

                LoadProbeConfigurations(_currentPartCode);

                // Use ordered probe measurements in exact database order
                var sortedProbeMeasurements = _orderedProbeMeasurements;

                foreach (var pm in sortedProbeMeasurements)
                {
                    pm.Readings.Clear();
                    pm.ReferenceValue = 0;
                }

                if (autoControl?.Bit == 0)
                {
                    await NotifyOnUIAsync("Load The Value in fixture...");
                    await WaitForValidProbeReadingAsync("100AY08P42");

                    //await NotifyOnUIAsync("Probe value detected for continuing...");
                    await NotifyOnUIAsync("PRESS START SWITCH TO START MASTERING");

                    while (GetPlcDeviceBit("X1") != 1)
                        await Task.Delay(100);

                    await NotifyOnUIAsync("START BUTTON PRESSED");
                }
                else
                {
                    await NotifyOnUIAsync("Press Robo start button to start mastering.");

                    while (GetPlcDeviceBit("M20") != 1)
                        await Task.Delay(100);

                    await NotifyOnUIAsync(" Robo start button Pressed");

                    SetPlcDevice("M101", 1);
                    await NotifyOnUIAsync(" Wating Robo for the Load Part...");

                    await WaitForValidProbeReadingAsync("100AY08P42");

                    await NotifyOnUIAsync("Waiting Robo for Safe position..");

                    while (GetPlcDeviceBit("M101") != 0)
                        await Task.Delay(100);
                }

                SetPlcDevice("M10", 1); // Motor Down

                NotifyStatus("Collecting the readings");


                while (GetPlcDeviceBit("X0") != 1)
                    await Task.Delay(100);

                SetPlcDevice("M14", 1); // MOTOR ON

                // wait for motor to stabilize
                while (_collectedReadings.TryDequeue(out _)) { }

                _plcProbeService.StartLiveReading();


                for (int i = 0; i < ArraySize; i++)
                {
                    if (Abort)
                    {
                        MessageBox.Show("Mastering aborted.", "Abort", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;
                    }

                    await Task.Delay(10); // keep loop async friendly

                    foreach (var pm in sortedProbeMeasurements)
                    {
                        var latest = _collectedReadings.Reverse().FirstOrDefault(r => r.ProbeId == pm.ProbeId);
                        pm.Readings.Add(!string.IsNullOrEmpty(latest.ProbeId) ? latest.Value : double.NaN);
                    }
                }

                _plcProbeService.StopLiveReading();
                SetPlcDevice("M14", 0); // MOTOR OFF
                SetPlcDevice("M10", 0); // Motor UP

                await NotifyOnUIAsync("Mastering Completed. Press Enter to Inspect the Master");

                // Calculate reference values
                foreach (var pm in sortedProbeMeasurements)
                {
                    var validReadings = pm.Readings.Where(r => !double.IsNaN(r)).ToList();
                    pm.ReferenceValue = (validReadings.Count > 0) ? validReadings.Max() : 0;
                }

                var MasterValues = sortedProbeMeasurements
                    .Where(pm => !string.IsNullOrEmpty(pm.Name))
                    .ToDictionary(pm => pm.Name!, pm => pm.ReferenceValue);

                OnCalculatedValuesReady(MasterValues);

                var probeMeasurementByName = sortedProbeMeasurements
                    .Where(pm => !string.IsNullOrEmpty(pm.Name))
                    .ToDictionary(pm => pm.Name!);

                if (IsMasteringStage)
                {
                    // Mastering stage
                    await HandleMasteringStageAsync(); // Master complete message already inside                    
                }
                else
                {
                    // Master Inspection stage
                    HandleMasterInspectionStage(probeMeasurementByName);


                    SetPlcDevice("M102", 1);

                    await NotifyOnUIAsync("Waiting Safe position from Robot");

                    while (GetPlcDeviceBit("M102") != 0)
                        await Task.Delay(100);
                    // ✅ Show Master Inspection complete message
                    await NotifyOnUIAsync("Master Inspection Completed");
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in MasterCheckProcedure: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            SetPlcDevice("M102", 1);

            await NotifyOnUIAsync("Waiting Safe position from Robot");

            while (GetPlcDeviceBit("M102") != 0)
                await Task.Delay(100);
            // ✅ Show Mastering Complete message here
            await NotifyOnUIAsync("Mastering Completed. Press Enter to Inspect the Master");
        }


        private bool IsValidParameter(string param, Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            return probeMeasurements.ContainsKey(param) && dbRefDict.ContainsKey(param);
        }

        private void HandleMasterInspectionStage(Dictionary<string, ProbeMeasurement> probeMeasurements)
        {
            var dbRefList = _dataStorageService.GetMasterProbeRef(_currentPartCode);
            var dbRefDict = dbRefList.ToDictionary(x => x.Name, x => x.Value);

            // ✅ Define all 12 parameters explicitly
            string[] dbNames = new string[]
            {
        "Overall Length", "Datum to End", "Head Diameter", "Groove Position",
        "Stem Dia Near Groove", "Stem Dia Near Undercut", "Groove Diameter",
        "Straightness", "End Face Runout", "Seat Height", "Seat Runout", "Datum to Groove"
            };

            var calculatedValues = new Dictionary<string, double>();

            // ✅ Compute each of the 12 parameters using existing formula dispatcher
            foreach (var dbName in dbNames)
            {
                try
                {
                    double val = CalculateProbeValue(dbName, probeMeasurements, dbRefDict);
                    calculatedValues[dbName] = val;
                }
                catch
                {
                    calculatedValues[dbName] = double.NaN;
                }
            }

            var resultsWithStatus = new Dictionary<string, ParameterResult>();
            bool overallOk = true;

            // ✅ Check tolerance & OK status for all 12
            foreach (var kvp in calculatedValues)
            {
                string param = kvp.Key;
                double val = kvp.Value;
                bool isOk = false;

                if (probeMeasurements.TryGetValue(param, out var probe))
                {
                    double lowerLimit = probe.MasterValue - probe.ToleranceMinus;
                    double upperLimit = probe.MasterValue + probe.TolerancePlus;
                    isOk = !double.IsNaN(val) && val >= lowerLimit && val <= upperLimit;
                }

                if (!isOk) overallOk = false;

                resultsWithStatus[param] = new ParameterResult { Value = val, IsOk = isOk };
            }

            // ✅ Update global status & fire event
            MasteringOK = overallOk;
            OnCalculatedValuesWithStatusReady(resultsWithStatus);

            // (Optional) Debug summary log
            System.Diagnostics.Debug.WriteLine("=== MASTER INSPECTION RESULTS ===");
            foreach (var kvp in resultsWithStatus)
                System.Diagnostics.Debug.WriteLine($"{kvp.Key}: {kvp.Value.Value:F4}  [{(kvp.Value.IsOk ? "OK" : "NG")}]");
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
            if (!probeMeasurements.TryGetValue("Seat Runout", out var pm)) return double.NaN;
            if (!dbRefDict.TryGetValue("Seat Runout", out var dbRefValue)) dbRefValue = 0.0;

            double offset = pm.ReferenceValue - dbRefValue;
            double runout = Math.Abs(offset);
            return runout == 0 ? 0.001 : runout;
        }

        private double CalculateDatumToGroove(Dictionary<string, ProbeMeasurement> probeMeasurements, Dictionary<string, double> dbRefDict)
        {
            double de = CalculateDatumToEnd(probeMeasurements, dbRefDict);
            double gp = CalculateGroovePosition(probeMeasurements, dbRefDict);

            if (double.IsNaN(de) || double.IsNaN(gp)) return double.NaN;
            return Math.Round(de - gp, 3);
        }

        #endregion


        private void LoadProbeConfigurations(string partCode)
        {
            _probeMeasurements.Clear();
            _orderedProbeMeasurements.Clear();

            var probeInstalls = _dataStorageService.GetProbeInstallByPartNumber(partCode);
            var masterVals = _dataStorageService.GetMasterReadingByPart(partCode);

            foreach (var probe in probeInstalls)
            {
                var master = masterVals?
                    .FirstOrDefault(m =>
                        string.Equals(m.Para_No, probe.ProbeId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m.Parameter, probe.Name, StringComparison.OrdinalIgnoreCase)
                    );

                double masterVal = master?.Nominal ?? 0;
                double tolPlus = master?.RTolPlus ?? 0;
                double tolMinus = master?.RTolMinus ?? 0;

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
    }
}
