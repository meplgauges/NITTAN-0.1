using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
namespace EVMS
{
    public partial class ProbeSetupPage : UserControl, INotifyPropertyChanged
    {
        private readonly string _connectionString;
        private readonly DispatcherTimer _timer;
        private readonly OrbitService _orbitService = new OrbitService();

        public ObservableCollection<string> PartNumbers { get; } = new();
        public ObservableCollection<ProbeRow> Probes { get; } = new();

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
                const string query = @"SELECT Parameter FROM PartConfig WHERE Para_No = @Para_No  AND ProbeStatus = 'Probe'";
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

                    var probe = Probes[index];
                    if (_orbitService.IsModuleConnected(probeId))
                    {
                        probe.ID = probeId;
                        probe.Stroke = stroke;
                    }
                    else
                    {
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
            if (_orbitService.ModulesById == null || _orbitService.ModulesById.Count == 0)
                return;

            double maxScale = 2.0;  // Scale max for full progress bar

            for (int i = 0; i < Probes.Count; i++)
            {
                var probe = Probes[i];
                if (probe == null || string.IsNullOrEmpty(probe.ID))
                    continue;

                if (!_orbitService.ModulesById.TryGetValue(probe.ID, out dynamic module))
                    continue;

                try
                {
                    double reading = (double)module.ReadingInUnits;

                    // Normalize for ProgressBar fill (0 to 100 scale)
                    double normalizedValue = (reading / maxScale) * 100;
                    probe.Value = Math.Min(Math.Max(normalizedValue, 0), 100);

                    // Show actual reading in status (no percentage)
                    if (reading > 1.600)
                    {
                        probe.Status = "OVER";
                        probe.InRange = false;
                    }
                    else if (reading < 0.370)
                    {
                        probe.Status = "UNDER";
                        probe.InRange = false;
                    }
                    else
                    {
                        probe.Status = $"{reading:0.000} mm";
                        probe.InRange = true;
                    }
                }
                catch
                {
                    probe.Value = 0;
                    probe.Status = "ERR";
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

            await LoadProbeDetailsFromInstallationDataAsync(SelectedPartNo);

            bool connected = await _orbitService.ConnectAsync();
            if (!connected)
            {
                MessageBox.Show("Failed to connect to Orbit Controller or no modules found.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var moduleDict = _orbitService.ModulesById;

            var missingIds = new List<string>();
            for (int i = 0; i < Probes.Count; i++)
            {
                var probe = Probes[i];
                if (probe == null) continue;

                if (string.IsNullOrEmpty(probe.ID) || !moduleDict.ContainsKey(probe.ID))
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

            MessageBox.Show($"{_orbitService.NetworkCount} Network(s) Found, {_orbitService.ModuleCount} Module(s) Connected.",
                "Orbit Connected", MessageBoxButton.OK, MessageBoxImage.Information);

            _timer.Start();
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            foreach (var probe in Probes)
            {
                probe.Value = 0;
                probe.ID = string.Empty;
                probe.Stroke = string.Empty;
                probe.Status = string.Empty;
                probe.InRange = false;
            }
            _orbitService.Disconnect();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public class ProbeRow : INotifyPropertyChanged
        {
            private string _title = string.Empty;
            private string _id = string.Empty;
            private string _stroke = string.Empty;
            private string _Status = string.Empty;

            private double _value;
            private bool _inRange;

            public string Title
            {
                get => _title;
                set => SetField(ref _title, value);
            }

            public string Status
            {
                get => _Status;
                set => SetField(ref _Status, value);
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
