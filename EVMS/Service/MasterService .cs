using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ActUtlTypeLib;   // Mitsubishi PLC COM library

namespace EVMS.Service
{
    public class MasterService
    {
        private readonly DataStorageService _dataStorageService;
        private readonly PlcProbeService _plcProbeService;

        // Mitsubishi PLC instance (for motor/relay bits)
        private readonly ActUtlType plc;

        // Raw probe readings
        private readonly List<(string ProbeId, double Value)> _collectedReadings = new();

        public MasterService()
        {
            _dataStorageService = new DataStorageService();
            _plcProbeService = new PlcProbeService();

            // Initialize Mitsubishi PLC driver
            plc = new ActUtlType
            {
                ActLogicalStationNumber = 1   // ⚠️ set this to your GX Works logical station number
            };
        }

        public bool IsConnected => _plcProbeService?.IsConnected ?? false;

        public List<PartEntryModel> GetActiveParts() =>
            _dataStorageService.GetActiveParts();

        public async Task<bool> EnsureConnectionAsync()
        {
            if (_plcProbeService == null)
                return false;

            if (!_plcProbeService.IsConnected)
            {
                bool connected = await _plcProbeService.ConnectAsync();
                if (connected)
                    _plcProbeService.ProbeValueUpdated += PlcProbeService_ProbeValueUpdated;
                return connected;
            }
            return true;
        }

        private void PlcProbeService_ProbeValueUpdated(object? sender, ProbeReadingEventArgs e)
        {
            lock (_collectedReadings)
            {
                _collectedReadings.Add((e.ModuleId, e.Value));
            }
        }

        public void StartLiveReading(int intervalMs = 10)
        {
            if (_plcProbeService == null || !_plcProbeService.IsConnected)
                throw new InvalidOperationException("Not connected to probe system");
            _plcProbeService.StartLiveReading(intervalMs);
        }

        public void StopLiveReading()
        {
            if (_plcProbeService == null) return;
            _plcProbeService.StopLiveReading();
            _plcProbeService.Disconnect();
            _plcProbeService.ProbeValueUpdated -= PlcProbeService_ProbeValueUpdated;
        }

        public async Task RunMasterCycleAsync(int liveReadIntervalMs = 10)
        {
            var activeParts = _dataStorageService.GetActiveParts();
            if (activeParts == null || activeParts.Count == 0)
            {
                MessageBox.Show("No active parts found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool connected = await EnsureConnectionAsync();
            if (!connected)
            {
                MessageBox.Show("Failed to connect to probe service.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _collectedReadings.Clear();

            try
            {
                //Ensure PLC connection
                int openResult = plc.Open();
                if (openResult != 0)
                    throw new Exception($"Failed to open PLC connection. Error code: {openResult}");

                // Start motor (M14 = ON)
                int result = plc.SetDevice("M14", 1);
                if (result != 0)
                    throw new Exception($"PLC error while starting motor: {result}");

                await Task.Delay(500); // motor warm-up

                //Start probe reading

                StartLiveReading(liveReadIntervalMs);
                await Task.Delay(liveReadIntervalMs * 100); // capture window

                // Stop probe reading
                StopLiveReading();

                // Stop motor (M14 = OFF)
                result = plc.SetDevice("M14", 0);
                if (result != 0)
                    throw new Exception($"PLC error while stopping motor: {result}");

                // Process readings
                Dictionary<string, List<double>> groupedReadings;
                lock (_collectedReadings)
                {
                    groupedReadings = _collectedReadings
                        .GroupBy(r => r.ProbeId)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Value).ToList());
                }

                if (groupedReadings.Count == 0)
                {
                    MessageBox.Show("No readings collected.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var processedResults = new Dictionary<string, double>();
                var lines = new List<string>();

                foreach (var kv in groupedReadings)
                {
                    string probeId = kv.Key;
                    List<double> values = kv.Value;
                    values.Sort();

                    // mid average
                    double midAverage = (values[Math.Max(5, values.Count / 4)] +
                                         values[Math.Max(0, values.Count - 5)]) / 2.0;

                    processedResults[probeId] = Math.Round(midAverage, 3);

                    lines.Add($"Probe {probeId}: Avg={midAverage:0.000}, Min={values.First():0.000}, Max={values.Last():0.000}");
                }

                // 🔹Show results
                MessageBox.Show(string.Join(Environment.NewLine, lines), "Processed Probe Readings",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Save to DB
                string partNumber = activeParts[0].Para_No ?? "";
                var probes = _dataStorageService.GetProbeInstallByPartNumber(partNumber);
                _dataStorageService.SaveProbeReadings(probes, partNumber, processedResults);

                MessageBox.Show("Master cycle completed and saved to DB.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during master cycle: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 🔹 Close PLC after cycle
                plc.Close();
            }
        }

    }
}
