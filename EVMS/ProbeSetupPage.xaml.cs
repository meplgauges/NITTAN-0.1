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

namespace EVMS
{
    public partial class ProbeSetupPage : UserControl, INotifyPropertyChanged
    {
        private readonly string _connectionString;
        private readonly DispatcherTimer _timer;

        public ObservableCollection<string> PartNumbers { get; } = new();
        public ObservableCollection<ProbeRow> Probes { get; } = new();

        private string _selectedPartNo;
        public string SelectedPartNo
        {
            get => _selectedPartNo;
            set
            {
                if (_selectedPartNo != value)
                {
                    _selectedPartNo = value;
                    RaisePropertyChanged();
                    _ = LoadProbesAsync(_selectedPartNo);
                }
            }
        }

        // Orbit objects
        private OrbitServer _orbServer;
        private OrbitNetwork _orbNet;
        private OrbitNetworks _orbNets;

        private OrbitModules _orbModules;

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
                MessageBox.Show($"Error loading Part Numbers: {ex.Message}");
            }
        }

        private async Task LoadProbesAsync(string partNo)
        {
            if (string.IsNullOrEmpty(partNo)) return;
            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                const string query = @"SELECT Name, ProbeId, Stroke 
                                       FROM ProbeInstallationData 
                                       WHERE PartNo = @PartNo AND Status = @Status";
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@PartNo", partNo);
                cmd.Parameters.AddWithValue("@Status", "Installed");
                using var reader = await cmd.ExecuteReaderAsync();
                Probes.Clear();
                while (await reader.ReadAsync())
                {
                    Probes.Add(new ProbeRow
                    {
                        Title = reader.GetString(0),
                        ID = reader.GetString(1),
                        Stroke = reader.GetString(2),
                        Value = 0,
                        Zero = 0,
                        InRange = false
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Probes: {ex.Message}");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_orbModules == null || _orbModules.Count == 0)
                return;

            int totalCount = Math.Min(Probes.Count, _orbModules.Count);

            for (int i = 0; i < totalCount; i++)
            {
                try
                {
                    double reading = _orbModules[i].ReadingInUnits;

                    // Convert 0-2 stroke reading to 0-100% value
                    double percentage = (reading / 2.0) * 100;

                    // Assign converted percentage to the probe’s Value property
                    Probes[i].Value = percentage;

                    // Update the UI ProgressBar (Bar) for this probe - 
                    // adjust if you have one ProgressBar per probe or single global one
                    //Application.Current.Dispatcher.Invoke(() =>
                    //{
                    //    Bar.Value = Math.Min(Math.Max(percentage, 0), 100);
                    //});
                }
                catch
                {
                    Probes[i].Value = 0;
                    Probes[i].InRange = false;

                    //Application.Current.Dispatcher.Invoke(() =>
                    //{
                    //    Bar.Value = 0;
                    //});
                }
            }
        }




        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedPartNo))
            {
                MessageBox.Show("Please select a Part Number first.");
                return;
            }

            await LoadProbesAsync(SelectedPartNo);

            try
            {
                // Step 1: Create Orbit server object if null
                if (_orbServer == null)
                    _orbServer = new OrbitServer();

                // Step 2: Connect if not already connected
                if (!_orbServer.Connected)
                    _orbServer.Connect();

                // Step 3: Verify connection success
                if (!_orbServer.Connected)
                {
                    MessageBox.Show("Failed to connect to Orbit Controller.",
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Step 4: Retrieve Orbit networks
                _orbNets = _orbServer.Networks;
                if (_orbNets == null || _orbNets.Count == 0)
                {
                    MessageBox.Show("No Orbit networks detected.",
                        "Network Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Step 5: Select the first available network
                _orbNet = _orbNets[0];
                if (_orbNet == null)
                {
                    MessageBox.Show("Failed to access Orbit network.",
                        "Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Step 6: Retrieve modules from network
                // After connecting and selecting network
                _orbModules = _orbNet.Modules;
                if (_orbModules == null || _orbModules.Count == 0)
                {
                    // Try to refresh module list
                    _orbModules.FindHotswapped();
                    if (_orbModules.Count == 0)
                    {
                        MessageBox.Show("No Orbit modules found in the selected network.",
                            "Module Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Step 7: Inform user and start timer for live probe updates
                MessageBox.Show(
                    $"{_orbNets.Count} Network(s) Found, {_orbModules.Count} Module(s) Connected.",
                    "Orbit Connected", MessageBoxButton.OK, MessageBoxImage.Information);

                _timer?.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Orbit connection failed: {ex.Message}",
                    "Exception", MessageBoxButton.OK, MessageBoxImage.Error);

                _orbServer = null;
                _orbNet = null;
                _orbModules = null;
            }
        }





        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            Probes.Clear();
            PartNumbers.Clear();

            _orbServer = null;
            _orbNet = null;
            _orbModules = null;
        }

        private void ZeroAllBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (var probe in Probes)
            {
                probe.Zero = probe.Value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public class ProbeRow : INotifyPropertyChanged
        {
            private string _title;
            private string _id;
            private string _stroke;
            private double _value;
            private double _zero;
            private bool _inRange;

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

            public double Zero
            {
                get => _zero;
                set => SetField(ref _zero, value);
            }

            public bool InRange
            {
                get => _inRange;
                set => SetField(ref _inRange, value);
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
            {
                if (Equals(field, value))
                    return false;
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return true;
            }
        }
    }
}
