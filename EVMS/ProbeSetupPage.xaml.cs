using System;
using System.Collections.ObjectModel;
using System.Configuration;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Solartron.Orbit3;
using System.Linq;
namespace EVMS
{
    public partial class ProbeSetupPage : UserControl, INotifyPropertyChanged
    {
        private readonly string _connectionString;
        private readonly DispatcherTimer _timer;
        public ObservableCollection<string> PartNumbers { get; } = new();
        public ObservableCollection<ProbeRow> Probes { get; } = new();
        private Dictionary<string, dynamic> _orbModuleById = new Dictionary<string, dynamic>();
        private string _selectedPartNo = string.Empty;
        public string SelectedPartNo
        {
            get => _selectedPartNo;
            set
            {
                if (_selectedPartNo != value)
                {
                    _selectedPartNo = value;
                    RaisePropertyChanged();
                    _ = LoadProbeNamesFromPartConfigAsync(_selectedPartNo);
                }
            }
        }
        // Orbit objects
        private OrbitServer? _orbServer;
        private OrbitNetwork? _orbNet;
        private OrbitNetworks? _orbNets;
        private OrbitModules? _orbModules;
        public ProbeSetupPage()
        {
            InitializeComponent();
            DataContext = this;
            _connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _timer.Tick += Timer_Tick;
            _ = LoadPartNumbersAsync();
        }
        private async Task LoadPartNumbersAsync()
        {
            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using var cmd = new SqlCommand("SELECT DISTINCT Para_No FROM PART_ENTRY ORDER BY Para_No", con);
                using var reader = await cmd.ExecuteReaderAsync();
                PartNumbers.Clear();
                while (await reader.ReadAsync())
                {
                    PartNumbers.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Part Numbers: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadProbeNamesFromPartConfigAsync(string partNo)
        {
            if (string.IsNullOrEmpty(partNo)) return;
            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                const string query = @"SELECT Parameter FROM PartConfig WHERE Para_No = @Para_No";
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Para_No", partNo);
                using var reader = await cmd.ExecuteReaderAsync();
                Probes.Clear();
                while (await reader.ReadAsync())
                {
                    Probes.Add(new ProbeRow
                    {
                        Title = reader.GetString(0),
                        ID = string.Empty,
                        Stroke = string.Empty,
                        Value = 0,
                        InRange = false
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading probe names: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadProbeDetailsFromInstallationDataAsync(string partNo)
        {
            if (string.IsNullOrEmpty(partNo)) return;
            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                const string query = @"SELECT ProbeId, Stroke 
                               FROM ProbeInstallationData 
                               WHERE PartNo = @PartNo AND Status = @Status";
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@PartNo", partNo);
                cmd.Parameters.AddWithValue("@Status", "Installed");
                using var reader = await cmd.ExecuteReaderAsync();
                int index = 0;
                while (await reader.ReadAsync() && index < Probes.Count)
                {
                    string probeId = reader.GetString(0);
                    string stroke = reader.GetString(1);
                    // ✅ only assign if this ProbeId exists in Orbit modules
                    if (_orbModuleById != null && _orbModuleById.ContainsKey(probeId))
                    {
                        var probe = Probes[index];
                        probe.ID = probeId;
                        probe.Stroke = stroke;
                    }
                    else
                    {
                        // clear values so UI shows nothing
                        var probe = Probes[index];
                        probe.ID = string.Empty;
                        probe.Stroke = string.Empty;
                    }
                    index++;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading probe details: {ex.Message}", "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Timer_Tick(object? sender, EventArgs? e)
        {
            if (_orbModuleById == null || _orbModuleById.Count == 0)
                return;
            for (int i = 0; i < Probes.Count; i++)
            {
                var probe = Probes[i];
                if (probe == null || string.IsNullOrEmpty(probe.ID))
                    continue;
                // ✅ Only update if the probe is connected (exists in the dictionary)
                if (!_orbModuleById.TryGetValue(probe.ID, out dynamic module))
                    continue;
                try
                {
                    double reading = (double)module.ReadingInUnits;
                    if (double.TryParse(probe.Stroke, out double strokeValue) && strokeValue > 0)
                    {
                        double percentage = (reading / strokeValue) * 100;
                        probe.Value = Math.Min(Math.Max(percentage, 0), 100);
                        probe.InRange = true;
                    }
                    else
                    {
                        probe.Value = 0;
                        probe.InRange = false;
                    }
                }
                catch
                {
                    probe.Value = 0;
                    probe.InRange = false;
                }
            }
        }
        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedPartNo))
            {
                MessageBox.Show("Please select a Part Number first.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Load probes from DB
            await LoadProbeDetailsFromInstallationDataAsync(SelectedPartNo);
            try
            {
                if (_orbServer == null)
                    _orbServer = new OrbitServer();
                if (!_orbServer.Connected)
                    _orbServer.Connect();
                if (!_orbServer.Connected)
                {
                    MessageBox.Show("Failed to connect to Orbit Controller.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _orbNets = _orbServer.Networks;
                if (_orbNets == null || _orbNets.Count == 0)
                {
                    MessageBox.Show("No Orbit networks detected.", "Network Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _orbNet = _orbNets[0];
                if (_orbNet == null)
                {
                    MessageBox.Show("Failed to access Orbit network.", "Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _orbModules = _orbNet.Modules;
                if (_orbModules == null || _orbModules.Count == 0)
                {
                    _orbModules?.FindHotswapped();
                    await Task.Delay(1000); // 200 ms delay - adjust as needed

                    if (_orbModules?.Count == 0)
                    {
                        MessageBox.Show("No Orbit modules found in the selected network.", "Module Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                // after you have assigned _orbModules = _orbNet.Modules and checked it's not null/empty
                _orbModuleById.Clear();
                // collect module IDs and build lookup using the indexer (no foreach / LINQ on OrbitModules)
                var moduleIds = new HashSet<string>();
                for (int i = 0; i < _orbModules.Count; i++)
                {
                    var module = _orbModules[i];
                    if (module == null) continue;
                    string id = module.ModuleID; // adjust if property name differs
                    if (!string.IsNullOrEmpty(id))
                    {
                        moduleIds.Add(id);
                        _orbModuleById[id] = module;
                    }
                }
                // verify DB probes exist in connected modules (no LINQ on OrbitModules)
                var missingIds = new List<string>();
                for (int i = 0; i < Probes.Count; i++)
                {
                    var probe = Probes[i];
                    if (probe == null) continue;
                    // if probe.ID is null/empty you may want to treat it as missing or skip
                    if (string.IsNullOrEmpty(probe.ID) || !moduleIds.Contains(probe.ID))
                    {
                        missingIds.Add(probe.ID ?? "<empty>");
                    }
                }
                if (missingIds.Count > 0)
                {
                    string missing = string.Join(", ", missingIds);
                    MessageBox.Show($"The following probes are not connected: {missing}",
                        "Probe Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _timer?.Start();
                    return;
                }
                // all good -> start
                MessageBox.Show($"{_orbNets.Count} Network(s) Found, {_orbModules?.Count} Module(s) Connected.",
                    "Orbit Connected", MessageBoxButton.OK, MessageBoxImage.Information);
                _timer?.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Orbit connection failed: {ex.Message}", "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                try
                {
                    if (_orbServer != null && _orbServer.Connected)
                        _orbServer.Disconnect();
                }
                catch
                {
                    // Ignore disconnect exceptions
                }
            }
        }
        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            foreach (var probe in Probes)
            {
                probe.Value = 0;
                probe.ID = string.Empty;
                probe.Stroke = string.Empty;
                probe.InRange = false;
            }
            try
            {
                if (_orbServer != null && _orbServer.Connected)
                {
                    _orbServer.Disconnect();

                }
                else
                {
                    MessageBox.Show("Orbit server is not connected.", "Disconnection Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during disconnection: {ex.Message}", "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public class ProbeRow : INotifyPropertyChanged
        {
            private string _title = string.Empty;
            private string _id = string.Empty;
            private string _stroke = string.Empty;
            private double _value;
            private bool _inRange;
            private string _statusText = string.Empty;
            public string StatusText
            {
                get => _statusText;
                set => SetField(ref _statusText, value);
            }
            public string Title
            {
                get => _title;
                set => SetField(ref _title, value);
            }
            public string ID
            {
                get => _id;
                set => SetField(ref _id, value);
            }
            public string Stroke
            {
                get => _stroke;
                set => SetField(ref _stroke, value);
            }
            public double Value
            {
                get => _value;
                set => SetField(ref _value, value);
            }
            public bool InRange
            {
                get => _inRange;
                set => SetField(ref _inRange, value);
            }
            public event PropertyChangedEventHandler? PropertyChanged;
            protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
            {
                if (Equals(field, value)) return false;
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return true;
            }
        }
    }
} 