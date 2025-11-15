using Microsoft.Data.SqlClient;
using Solartron.Orbit3;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EVMS
{
    public partial class ProbeInstallPage : UserControl, INotifyPropertyChanged
    {
        // Orbit3 hardware objects - mark nullable since initialized later
        public event Action<string>? StatusMessageChanged;

        private OrbitServer? _orbServer;
        private OrbitNetwork? _orbNet;
        private OrbitNetworks? _orbNets;
        private OrbitModules? _orbModules;

        private readonly string connectionString;
        public ObservableCollection<ProbeViewModel> Probes { get; set; }
        public ObservableCollection<string> PartNumbers { get; set; }

        private string? _selectedPartNo; // nullable as may be empty initially
        public string? SelectedPartNo
        {
            get => _selectedPartNo;
            set
            {
                if (_selectedPartNo != value)
                {
                    _selectedPartNo = value;
                    OnPropertyChanged();
                    _ = LoadProbesForPartAsync(_selectedPartNo ?? string.Empty);
                }
            }
        }

      

        public ProbeInstallPage()
        {
            connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"]?.ConnectionString ?? throw new InvalidOperationException("Connection string missing");
            InitializeComponent();
            Probes = new ObservableCollection<ProbeViewModel>();
            PartNumbers = new ObservableCollection<string>();
            DataContext = this;
            Loaded += ProbeInstallPage_Loaded;
            Unloaded += ProbeInstallPage_Unloaded;
            this.Loaded += SettingsPage_Loaded;

            // ✅ Register ESC key handler
            this.PreviewKeyDown += SettingsPage_PreviewKeyDown;

            // Initialize as null, assigned later on connect
            _orbServer = null!;
            _orbNet = null!;
            _orbNets = null!;
            _orbModules = null!;
            _selectedPartNo = string.Empty;
        }

        // ✅ ESC key detection
        private void SettingsPage_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                HandleEscKeyAction();
                e.Handled = true;
            }
        }


        private void SettingsPage_Loaded(object? sender, RoutedEventArgs e)
        {
            // Ask WPF to focus this control (deferred)
            this.Focusable = true;
            this.IsTabStop = true;

            // Try several ways to set keyboard focus
            Keyboard.Focus(this);                                  // set logical focus
            FocusManager.SetFocusedElement(Window.GetWindow(this)!, this); // set focused element on window

        }
        // ✅ Handles ESC key press to go back to HomePage
        private void HandleEscKeyAction()
        {
            NotifyStatus(".");

            DisconnectOrbit();
            Window currentWindow = Window.GetWindow(this);
            if (currentWindow != null)
            {
                var mainContentGrid = currentWindow.FindName("MainContentGrid") as Grid;
                if (mainContentGrid != null)
                {
                    mainContentGrid.Children.Clear();

                    var resultPage = new Dashboard
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };

                    mainContentGrid.Children.Add(resultPage);
                }
            }
        }


        private void NotifyStatus(string message)
        {
            StatusMessageChanged?.Invoke(message);
        }

        // ================= ORBIT CONNECTION =================
        private async void ProbeInstallPage_Loaded(object sender, RoutedEventArgs e)
        {
            bool connected = await ConnectOrbitAsync();
            if (connected)
                NotifyStatus("Connected to Probes.");
            //MessageBox.Show($"{_orbNets?.Count ?? 0} Networks Found. Connected to Orbit.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                NotifyStatus("Failed to connect to Orbit.");

            await LoadPartNumbersAsync();
            if (PartNumbers.Count > 0)
                SelectedPartNo = PartNumbers[0];
        }

        private async Task<bool> ConnectOrbitAsync()
        {
            try
            {
                _orbServer ??= new OrbitServer();

                if (!_orbServer.Connected)
                    _orbServer.Connect();

                if (!_orbServer.Connected)
                    return false;

                _orbNets = _orbServer.Networks;
                if (_orbNets == null || _orbNets.Count == 0)
                    return false;

                _orbNet = _orbNets[0];
                if (_orbNet == null)
                    return false;

                _orbModules = _orbNet.Modules;
                return _orbModules != null;
            }
            catch
            {
                return false;
            }
        }

        private void DisconnectOrbit()
        {
            try
            {
                if (_orbServer != null && _orbServer.Connected)
                    _orbServer.Disconnect();
            }
            catch { }
        }

        // ================= DATABASE FUNCTIONS =================
        private async Task LoadPartNumbersAsync()
        {
            try
            {
                var partNumbers = new List<string>();
                using (var con = new SqlConnection(connectionString))
                {
                    await con.OpenAsync();
                    string query = "SELECT DISTINCT Para_No FROM PART_ENTRY ORDER BY Para_No";
                    using (var cmd = new SqlCommand(query, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            partNumbers.Add(reader.GetString(0));
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PartNumbers.Clear();
                    foreach (var pn in partNumbers)
                        PartNumbers.Add(pn);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Part Numbers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task LoadProbesForPartAsync(string partNo)
        {
            if (string.IsNullOrEmpty(partNo)) return;
            var loadedProbes = new List<ProbeViewModel>();
            try
            {
                using (var con = new SqlConnection(connectionString))
                {
                    await con.OpenAsync();
                    string query = @"
                        SELECT pc.SrNo, pc.Para_No, pc.Parameter, 
                               ISNULL(pid.ProbeId, '--'), ISNULL(pid.Stroke, '--'), ISNULL(pid.Status, 'Pending')
                        FROM PartConfig pc
                        LEFT JOIN ProbeInstallationData pid 
                               ON pc.Para_No = pid.PartNo AND pc.Parameter = pid.Name
                        WHERE pc.Para_No = @PartNo AND pc.ProbeStatus = 'Probe'
                        ORDER BY pc.SrNo";
                    using (var cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@PartNo", partNo);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            int counter = 1;
                            while (await reader.ReadAsync())
                            {
                                loadedProbes.Add(new ProbeViewModel
                                {
                                    No = counter,
                                    Name = reader.GetString(2),
                                    ProbeName = $"Probe {counter}",
                                    ProbeId = reader.GetString(3),
                                    Stroke = reader.GetString(4),
                                    Status = reader.GetString(5) == "Installed" ? "Installed" : "Pending"
                                });
                                counter++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Error loading probes for part: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                Probes.Clear();
                foreach (var probe in loadedProbes)
                {
                    Probes.Add(probe);
                }
            });
        }

        private bool ProbeIdAlreadyAssignedToPart(string partNo, string probeId)
        {
            if (string.IsNullOrWhiteSpace(probeId) || probeId == "--") return false;
            try
            {
                using var con = new SqlConnection(connectionString);
                con.Open();
                string query = "SELECT COUNT(*) FROM ProbeInstallationData WHERE PartNo = @PartNo AND ProbeId = @ProbeId";
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@PartNo", partNo);
                cmd.Parameters.AddWithValue("@ProbeId", probeId);
                int count = (int)cmd.ExecuteScalar();
                return count > 0;
            }
            catch
            {
                return true;
            }
        }

        private void SaveModelData(string partNo, string name, string probeId, string stroke, string status)
        {
            if (string.IsNullOrWhiteSpace(probeId) || probeId == "--") return;
            try
            {
                if (ProbeIdAlreadyAssignedToPart(partNo, probeId))
                {
                    MessageBox.Show("This probe is already installed on the selected part number and cannot be installed again.",
                        "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                using var con = new SqlConnection(connectionString);
                con.Open();
                string insertQuery = @"
                    INSERT INTO ProbeInstallationData (PartNo, Name, ProbeId, Stroke, Status)
                    VALUES (@PartNo, @Name, @ProbeId, @Stroke, @Status)";
                using var insertCmd = new SqlCommand(insertQuery, con);
                insertCmd.Parameters.AddWithValue("@PartNo", partNo);
                insertCmd.Parameters.AddWithValue("@Name", name);
                insertCmd.Parameters.AddWithValue("@ProbeId", probeId);
                insertCmd.Parameters.AddWithValue("@Stroke", stroke);
                insertCmd.Parameters.AddWithValue("@Status", status);
                insertCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving model data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= BUTTON HANDLERS =================
        private void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_orbServer == null || !_orbServer.Connected)
            {
                MessageBox.Show("Orbit hardware not connected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (sender is Button btn && btn.DataContext is ProbeViewModel probe)
            {
                if (probe.Status == "Installed")
                {
                    MessageBox.Show("This probe is already installed for this parameter.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btn.IsEnabled = false;
                try
                {
                    NotifyStatus("Move the probe now to be detected.");

                    // --- Wait for probe signal ---
                    bool added = _orbModules?.NotifyAddModule() ?? true;
                    if (!added)
                    {
                        MessageBox.Show("No new probe detected or operation cancelled.",
                                        "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // --- Get last added module ---
                    var mod = _orbModules[_orbModules.Count - 1];
                    if (mod != null)
                    {
                        string probeId = mod.ModuleID;
                        string stroke = mod.Stroke.ToString();

                        if (ProbeIdAlreadyAssignedToPart(SelectedPartNo, probeId))
                        {
                            MessageBox.Show("This probe is already installed for this part number.",
                                            "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Save in DB
                        SaveModelData(SelectedPartNo, probe.Name, probeId, stroke, "Installed");

                        // Update UI
                        probe.ProbeId = probeId;
                        probe.Stroke = stroke;
                        probe.Status = "Installed";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error during probe install: {ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btn.IsEnabled = true;
                }
            }
        }

        private async void ResetAllProbes_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to reset all saved probes?",
                                         "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using (var con = new SqlConnection(connectionString))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand("DELETE FROM ProbeInstallationData", con))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // ✅ Reload probes for the selected part number instead of blank UI
                await LoadProbesForPartAsync(SelectedPartNo);

                NotifyStatus("Reset Successful");
                          
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting probes: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }


        private void ProbeInstallPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DisconnectOrbit();
        }

        // ================= NOTIFY PROPERTY CHANGED =================
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ================= VIEWMODEL =================
    public class ProbeViewModel : INotifyPropertyChanged
    {
        private int _no;
        private string _name = string.Empty;          // Initialize to empty string to avoid nulls
        private string _probeName = string.Empty;
        private string _probeId = string.Empty;
        private string _stroke = string.Empty;
        private string _status = string.Empty;

        public int No { get => _no; set { _no = value; OnPropertyChanged(); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string ProbeName { get => _probeName; set { _probeName = value; OnPropertyChanged(); } }
        public string ProbeId { get => _probeId; set { _probeId = value; OnPropertyChanged(); } }
        public string Stroke { get => _stroke; set { _stroke = value; OnPropertyChanged(); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        public bool IsInstalled => Status == "Installed";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
