using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace EVMS
{
    public partial class ProbeInstallPage : UserControl, INotifyPropertyChanged
    {
        private readonly OrbitService _orbitService = new OrbitService();
        private readonly string connectionString;

        public ObservableCollection<ProbeViewModel> Probes { get; set; }
        public ObservableCollection<string> PartNumbers { get; set; }

        private string _selectedPartNo = string.Empty;
        public string SelectedPartNo
        {
            get => _selectedPartNo;
            set
            {
                if (_selectedPartNo != value)
                {
                    _selectedPartNo = value;
                    OnPropertyChanged();
                    _ = LoadProbesForPartAsync(_selectedPartNo);
                }
            }
        }

        public ProbeInstallPage()
        {
            connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"]?.ConnectionString
                ?? throw new InvalidOperationException("Connection string EVMSDb missing.");
            InitializeComponent();
            Probes = new ObservableCollection<ProbeViewModel>();
            PartNumbers = new ObservableCollection<string>();
            DataContext = this;
            Loaded += ProbeInstallPage_Loaded;
            Unloaded += ProbeInstallPage_Unloaded;
        }

        private async void ProbeInstallPage_Loaded(object sender, RoutedEventArgs e)
        {
            bool connected = await _orbitService.ConnectAsync();
            if (!connected)
            {
                MessageBox.Show("Failed to connect to Orbit Controller or no modules detected.",
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            await LoadPartNumbersAsync();
            if (PartNumbers.Count > 0)
                SelectedPartNo = PartNumbers[0];
            await SyncConnectedModulesWithDatabaseAsync();
        }

        private void ProbeInstallPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _orbitService.Disconnect();
        }

        private bool IsNetworkAvailable() => NetworkInterface.GetIsNetworkAvailable();

        private async Task LoadPartNumbersAsync()
        {
            try
            {
                var partNumbers = new List<string>();
                using var con = new SqlConnection(connectionString);
                await con.OpenAsync();
                string query = "SELECT DISTINCT Para_No FROM PART_ENTRY ORDER BY Para_No";
                using var cmd = new SqlCommand(query, con);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
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

        private async Task LoadPartInstalledProbeIdsAsync(string partNo, HashSet<string> installedIds)
        {
            try
            {
                using var con = new SqlConnection(connectionString);
                await con.OpenAsync();
                string query = "SELECT ProbeId FROM ProbeInstallationData WHERE PartNo = @PartNo AND ProbeId IS NOT NULL AND ProbeId != '--'";
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@PartNo", partNo);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    installedIds.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading installed probes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task LoadProbesForPartAsync(string partNo)
        {
            if (string.IsNullOrEmpty(partNo))
                return;
            bool networkAvailable = IsNetworkAvailable();
            var loadedProbes = new List<ProbeViewModel>();
            try
            {
                using var con = new SqlConnection(connectionString);
                await con.OpenAsync();
                if (!networkAvailable || !_orbitService.IsConnected)
                {
                    string query = "SELECT SrNo, Para_No, Parameter FROM PartConfig WHERE Para_No = @PartNo AND ProbeStatus = 'Probe' ORDER BY SrNo";
                    using var cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@PartNo", partNo);
                    using var reader = await cmd.ExecuteReaderAsync();
                    int counter = 1;
                    while (await reader.ReadAsync())
                    {
                        string name = reader.GetString(2);
                        loadedProbes.Add(new ProbeViewModel
                        {
                            No = counter++,
                            Name = name,
                            ProbeName = $"Probe {counter - 1}",
                            ProbeId = "--",
                            Stroke = "--",
                            Status = "Not Connected"
                        });
                    }
                }
                else
                {
                    var installedProbesDB = new HashSet<string>();
                    await LoadPartInstalledProbeIdsAsync(partNo, installedProbesDB);
                    string query = @"
                        SELECT pc.SrNo, pc.Para_No, pc.Parameter,
                               ISNULL(pid.ProbeId, '--') AS ProbeId,
                               ISNULL(pid.Stroke, '--') AS Stroke,
                               ISNULL(pid.Status, 'Pending') AS Status
                        FROM PartConfig pc
                        LEFT JOIN ProbeInstallationData pid ON pc.Para_No = pid.PartNo AND pc.Parameter = pid.Name
                        WHERE pc.Para_No = @PartNo AND pc.ProbeStatus = 'Probe'
                        ORDER BY pc.SrNo";
                    using var cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@PartNo", partNo);
                    using var reader = await cmd.ExecuteReaderAsync();
                    int counter = 1;
                    var foundIds = new HashSet<string>();
                    while (await reader.ReadAsync())
                    {
                        string name = reader.GetString(2);
                        string probeId = reader.GetString(3);
                        string stroke = reader.GetString(4);
                        string status = reader.GetString(5);
                        if (!string.IsNullOrWhiteSpace(probeId) && probeId != "--" && foundIds.Contains(probeId))
                            continue;
                        if (!string.IsNullOrWhiteSpace(probeId) && probeId != "--")
                            foundIds.Add(probeId);
                        status = installedProbesDB.Contains(probeId) ? "Installed" : "Pending";
                        loadedProbes.Add(new ProbeViewModel
                        {
                            No = counter++,
                            Name = name,
                            ProbeName = $"Probe {counter - 1}",
                            ProbeId = probeId,
                            Stroke = stroke,
                            Status = status
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Error loading probes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Probes.Clear();
                foreach (var probe in loadedProbes)
                    Probes.Add(probe);
            });
        }

        private async Task SyncConnectedModulesWithDatabaseAsync()
        {
            if (!_orbitService.IsConnected)
                return;
            var installedProbeIdsDB = new HashSet<string>();
            try
            {
                using var con = new SqlConnection(connectionString);
                await con.OpenAsync();
                string query = "SELECT DISTINCT ProbeId FROM ProbeInstallationData WHERE ProbeId IS NOT NULL AND ProbeId != '--'";
                using var cmd = new SqlCommand(query, con);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    installedProbeIdsDB.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Error loading installed probe IDs from DB: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                return;
            }
            try
            {
                _orbitService.RefreshModules();
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Error refreshing Orbit modules: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                return;
            }
            var connectedIds = _orbitService.GetConnectedModuleIds();
            MessageBox.Show($"Detected {connectedIds.Count} modules: {string.Join(", ", connectedIds)}");
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var probe in Probes)
                {
                    if (!string.IsNullOrWhiteSpace(probe.ProbeId) && probe.ProbeId != "--")
                    {
                        if (connectedIds.Contains(probe.ProbeId) && installedProbeIdsDB.Contains(probe.ProbeId))
                            probe.Status = "Installed";
                        else if (!connectedIds.Contains(probe.ProbeId))
                            probe.Status = "Not Connected";
                        else
                            probe.Status = "Pending";
                    }
                    else
                    {
                        probe.Status = "Pending";
                    }
                }
                foreach (var id in connectedIds)
                {
                    if (!installedProbeIdsDB.Contains(id))
                    {
                        bool exists = false;
                        foreach (var p in Probes)
                        {
                            if (p.ProbeId == id)
                            {
                                exists = true;
                                break;
                            }
                        }
                        if (!exists)
                        {
                            Probes.Add(new ProbeViewModel
                            {
                                No = Probes.Count + 1,
                                Name = "New Hardware",
                                ProbeName = $"Probe {Probes.Count + 1}",
                                ProbeId = id,
                                Stroke = "--",
                                Status = "New"
                            });
                        }
                    }
                }
            });
        }

        private void SaveModelData(string partNo, string name, string probeId, string stroke, string status)
        {
            if (string.IsNullOrWhiteSpace(probeId) || probeId == "--")
                return;
            try
            {
                if (ProbeIdAlreadyAssigned(partNo, probeId))
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
                using var cmd = new SqlCommand(insertQuery, con);
                cmd.Parameters.AddWithValue("@PartNo", partNo);
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@ProbeId", probeId);
                cmd.Parameters.AddWithValue("@Stroke", stroke);
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving model data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ProbeIdAlreadyAssigned(string partNo, string probeId)
        {
            if (string.IsNullOrWhiteSpace(probeId) || probeId == "--")
                return false;
            try
            {
                using var con = new SqlConnection(connectionString);
                con.Open();
                string query = "SELECT COUNT(*) FROM ProbeInstallationData WHERE PartNo = @PartNo AND ProbeId = @ProbeId";
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@PartNo", partNo);
                cmd.Parameters.AddWithValue("@ProbeId", probeId);
                int count = (int)cmd.ExecuteScalar()!;
                return count > 0;
            }
            catch
            {
                return true;
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
                using var con = new SqlConnection(connectionString);
                await con.OpenAsync();
                using var cmd = new SqlCommand("DELETE FROM ProbeInstallationData", con);
                await cmd.ExecuteNonQueryAsync();
                _orbitService.ClearModules();
                Probes.Clear();
                MessageBox.Show("All probes have been reset.", "Reset Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting probes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_orbitService.IsConnected)
            {
                MessageBox.Show("Orbit modules not initialized. Please connect first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (sender is Button btn && btn.DataContext is ProbeViewModel probe)
            {
                if (probe.Status == "Installed")
                {
                    MessageBox.Show("This probe is already installed at this parameter.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btn.IsEnabled = false;
                try
                {
                    MessageBox.Show("Please move the probe now to be detected. Press ESC to cancel.",
                        "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
                    bool addedModule = await Task.Run(() => _orbitService.NotifyAddModule());
                    if (!addedModule)
                    {
                        MessageBox.Show("No new probe detected or operation cancelled.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var newModule = _orbitService.GetLastModule();
                    dynamic mod = newModule;
                    SaveModelData(SelectedPartNo, probe.Name, mod.ModuleID, mod.Stroke.ToString(), "Installed");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        probe.ProbeId = mod.ModuleID;
                        probe.Stroke = mod.Stroke.ToString();
                        probe.Status = "Installed";
                        probe.ProbeName = $"Probe {probe.No}";
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during probe detection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btn.IsEnabled = true;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ProbeViewModel : INotifyPropertyChanged
    {
        private int _no;
        private string _name = string.Empty;
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
