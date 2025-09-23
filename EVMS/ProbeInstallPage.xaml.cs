using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Solartron.Orbit3;

namespace EVMS
{
    public partial class ProbeInstallPage : UserControl, INotifyPropertyChanged
    {
        private readonly OrbitService _orbitService = new();
        private readonly string connectionString;
        public ObservableCollection<ProbeViewModel> Probes { get; set; }
        public ObservableCollection<string> PartNumbers { get; set; }
        private string _selectedPartNo;
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
            connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;
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
            if (connected)
                MessageBox.Show($"{_orbitService.NetworkCount} Networks Found. Connected to Orbit.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Failed to connect to Orbit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            await LoadPartNumbersAsync();
            if (PartNumbers.Count > 0)
                SelectedPartNo = PartNumbers[0];
        }

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
        public void SaveModelData(string partNo, string name, string probeId, string stroke, string status)
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

        private async void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_orbitService.IsConnected)
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
                    MessageBox.Show("Move the probe now to be detected. Press ESC to cancel.", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
                    bool added = _orbitService.NotifyAddModule();
                    if (!added)
                    {
                        MessageBox.Show("No new probe detected or operation cancelled.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    var mod = _orbitService.GetLastModule();
                    if (mod != null)
                    {
                        SaveModelData(SelectedPartNo, probe.Name, mod.ModuleID, mod.Stroke.ToString(), "Installed");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            probe.ProbeId = mod.ModuleID;
                            probe.Stroke = mod.Stroke.ToString();
                            probe.Status = "Installed";
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error during probe install: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                _orbitService.ClearModules();
                Probes?.Clear();
                MessageBox.Show("All probes have been reset.", "Reset Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting probes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProbeInstallPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _orbitService?.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ProbeViewModel : INotifyPropertyChanged
    {
        private int _no;
        private string? _name;
        private string? _probeName;
        private string? _probeId;
        private string? _stroke;
        private string? _status;
        public int No { get => _no; set { _no = value; OnPropertyChanged(); } }
        public string? Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string? ProbeName { get => _probeName; set { _probeName = value; OnPropertyChanged(); } }
        public string? ProbeId { get => _probeId; set { _probeId = value; OnPropertyChanged(); } }
        public string? Stroke { get => _stroke; set { _stroke = value; OnPropertyChanged(); } }
        public string? Status { get => _status; set { _status = value; OnPropertyChanged(); } }
        public bool IsInstalled => Status == "Installed";
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
