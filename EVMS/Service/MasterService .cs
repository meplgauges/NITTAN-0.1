using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ActUtlTypeLib;

namespace EVMS.Service
{
    public class ProbeMeasurement
    {
        public string ProbeId { get; set; }
        public List<double> Readings { get; } = new List<double>();
        public double ReferenceValue { get; set; } = 0;
        public double MasterValue { get; set; } = 0;
        public double TolerancePlus { get; set; }
        public double ToleranceMinus { get; set; }
    }

    public class MasterService : IDisposable
    {
        private readonly ActUtlType plc;
        private readonly DataStorageService _dataStorageService;
        private readonly PlcProbeService _plcProbeService;

        private Dictionary<string, ProbeMeasurement> _probeMeasurements = new Dictionary<string, ProbeMeasurement>();
        private const int SampleCount = 150;
        private bool _isMasteringStage = true;

        private readonly List<(string ProbeId, double Value)> _collectedReadings = new List<(string, double)>();

        private int _currentOperationalMode;
        private int _currentAutoRotateState;
        private string _currentPartCode = "";
        private string _currentMotorDirection = "Normal";

        public MasterService()
        {
            _dataStorageService = new DataStorageService();
            _plcProbeService = new PlcProbeService();
            plc = new ActUtlType { ActLogicalStationNumber = 1 };

            _plcProbeService.ProbeValueUpdated += ProbeValueUpdatedHandler;
        }

        public bool IsConnected => _plcProbeService?.IsConnected ?? false;

        public async Task<bool> EnsureConnectionAsync()
        {
            if (_plcProbeService == null)
                return false;

            if (!_plcProbeService.IsConnected)
            {
                bool connected = await _plcProbeService.ConnectAsync();
                if (connected)
                {
                    _plcProbeService.ProbeValueUpdated += ProbeValueUpdatedHandler;

                    int openResult = plc.Open();
                    if (openResult != 0)
                    {
                        MessageBox.Show(
                            $"Failed to open PLC connection. Error code: {openResult}",
                            "PLC Init Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }
                return connected;
            }
            return true;
        }

        public void Cleanup()
        {
            _plcProbeService.StopLiveReading();
            _plcProbeService?.Disconnect();
            _plcProbeService.ProbeValueUpdated -= ProbeValueUpdatedHandler;
            plc.Close();
        }

        private void ProbeValueUpdatedHandler(object? sender, ProbeReadingEventArgs e)
        {
            lock (_collectedReadings)
            {
                _collectedReadings.Add((e.ModuleId, e.Value));
            }
        }

        private bool SetPlcDevice(string device, int value)
        {
            int ret = plc.SetDevice(device, (short)value);
            if (ret != 0)
            {
                MessageBox.Show(
                    $"SetDevice failed: Device={device}, Code={ret}",
                    "PLC Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private int GetPlcDevice(string device)
        {
            int ret = plc.GetDevice(device, out int value);
            if (ret != 0)
            {
                MessageBox.Show(
                    $"GetDevice failed: Device={device}, Code={ret}",
                    "PLC Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return -1;
            }
            return value;
        }

        public async Task MasterCheckProcedureAsync()
        {
            try
            {
                var activeParts = _dataStorageService.GetActiveParts();
                if (activeParts == null || activeParts.Count == 0)
                {
                    MessageBox.Show("No active parts found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _currentPartCode = activeParts[0].Para_No ?? "";
                if (string.IsNullOrEmpty(_currentPartCode))
                {
                    MessageBox.Show("Active part code is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // [Initialize Mastering] - Load configs
                LoadProbeConfigurations(_currentPartCode);

                foreach (var pm in _probeMeasurements.Values)
                    pm.Readings.Clear();

                // Ensure connection to PLC and probe services
                bool ok = await EnsureConnectionAsync();
                if (!ok) return;

                // [Control PLC: Motor ON]
                ok = SetPlcDevice("M14", 1);
                if (!ok) return;

                _collectedReadings.Clear();

                // [Collect Raw Probe Readings]
                _plcProbeService.StartLiveReading();

                // Wait until sufficient samples collected (SampleCount)
                while (true)
                {
                    int currentSampleCount;
                    lock (_collectedReadings)
                    {
                        currentSampleCount = _collectedReadings.Count;
                    }
                    if (currentSampleCount >= SampleCount)
                        break;

                    await Task.Delay(10); // Small pause to reduce CPU load
                }

                _plcProbeService.StopLiveReading();

                // [Control PLC: Motor OFF]
                ok = SetPlcDevice("M14", 0);
                if (!ok) return;

                // [Process Readings]
                Dictionary<string, List<double>> groupedReadings;
                lock (_collectedReadings)
                {
                    groupedReadings = _collectedReadings
                        .GroupBy(r => r.ProbeId)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Value).ToList());
                }

                foreach (var pm in _probeMeasurements.Values)
                {
                    if (groupedReadings.TryGetValue(pm.ProbeId, out List<double> readings))
                    {
                        pm.Readings.AddRange(readings);
                        pm.Readings.Sort();

                        int n = pm.Readings.Count;
                        // Compute mid-average to get a stable Master Reference value
                        pm.ReferenceValue = Math.Round(
                            (pm.Readings[Math.Max(5, n / 4)] + pm.Readings[Math.Max(0, n - 5)]) / 2.0, 3);
                    }
                }

                // [Update Master Reference Data]
                if (_isMasteringStage)
                {
                    foreach (var pm in _probeMeasurements.Values)
                    {
                        _dataStorageService.SaveProbeReadings(
                            _dataStorageService.GetProbeInstallByPartNumber(_currentPartCode),
                            _currentPartCode,
                            new Dictionary<string, double> { { pm.ProbeId, pm.ReferenceValue } }
                        );
                    }

                    //_dataStorageService.UpdateMasterExpiration(DateTime.Now, _currentPartCode);

                   // MessageBox.Show("Mastering completed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    _isMasteringStage = false;
                }
                else
                {
                    // Inspection mode handling can be added here if needed
                }
                // [Mastering Complete]
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error in MasterCheckProcedure: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<double> ReadProbeOnceAsync(string probeId)
        {
            lock (_collectedReadings)
            {
                var latest = _collectedReadings.LastOrDefault(r => r.ProbeId == probeId);
                if (!latest.Equals(default((string, double))))
                {
                    return latest.Value;
                }
            }
            return double.NaN;
        }

        private void LoadProbeConfigurations(string partCode)
        {
            _probeMeasurements.Clear();
            var probeInstalls = _dataStorageService.GetProbeInstallByPartNumber(partCode);
            var masterVals = _dataStorageService.GetMasterReadingByPart(partCode);

            foreach (var probe in probeInstalls)
            {
                var master = masterVals?.FirstOrDefault(m => m.Para_No == probe.ProbeId);
                double masterVal = master?.Nominal ?? 0;
                double tolPlus = master?.RTolPlus ?? 0;
                double tolMinus = master?.RTolMinus ?? 0;

                _probeMeasurements[probe.ProbeId] = new ProbeMeasurement
                {
                    ProbeId = probe.ProbeId,
                    MasterValue = masterVal,
                    TolerancePlus = tolPlus,
                    ToleranceMinus = tolMinus
                };
            }
        }

        private void UpdateProbeUI(string probeId, double value, bool isPass)
        {
            // Add UI update logic here.
        }

        public void Dispose()
        {
            Cleanup();
            _dataStorageService?.Dispose();
        }
    }
}
