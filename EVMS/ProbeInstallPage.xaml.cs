using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Data.SqlClient;
using OrbitCOM;

namespace EVMS
{
    public partial class ProbeInstallPage : UserControl, INotifyPropertyChanged
    {
        private OrbitServerClass? _orbServer;
        private OrbitNetworks? _orbNets;
        private OrbitNetwork? _orbNet;
        private OrbitModules? _orbModules;
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
            connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;
            InitializeComponent();
            Probes = new ObservableCollection<ProbeViewModel>();
            PartNumbers = new ObservableCollection<string>();
            DataContext = this;
            Loaded += ProbeInstallPage_Loaded;
            Unloaded += ProbeInstallPage_Unloaded;
        }

        private void ProbeInstallPage_Loaded(object sender, RoutedEventArgs e)
        {
            ConnectToOrbit();
            _ = LoadPartNumbersAsync();

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
                    {
                        while (await reader.ReadAsync())
                            partNumbers.Add(reader.GetString(0));
                    }
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

        private void ConnectToOrbit()
        {
            try
            {
                _orbServer = new OrbitServerClass();
                if (!_orbServer.Connected)
                    _orbServer.Connect();

                if (_orbServer.Connected)
                {
                    _orbNets = _orbServer.Networks;
                    if (_orbNets != null && _orbNets.Count > 0)
                    {
                        _orbNet = _orbNets[0];
                        _orbModules = _orbNet.Modules;
                        MessageBox.Show($"{_orbNets.Count} Networks Found. Connected to Orbit.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                        MessageBox.Show("No networks found in Orbit.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show("Failed to connect to Orbit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while connecting to Orbit: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsNetworkAvailable() => NetworkInterface.GetIsNetworkAvailable();

        private async Task LoadInstalledProbeIdsAsync(string partNo, HashSet<string> installedProbes)
        {
            try
            {
                using (var con = new SqlConnection(connectionString))
                {
                    await con.OpenAsync();
                    string installedQuery = @"
                        SELECT ProbeId FROM ProbeInstallationData
                        WHERE PartNo = @PartNo AND ProbeId IS NOT NULL AND ProbeId != '--'";
                    using (var cmdInstalled = new SqlCommand(installedQuery, con))
                    {
                        cmdInstalled.Parameters.AddWithValue("@PartNo", partNo);
                        using (var readerInstalled = await cmdInstalled.ExecuteReaderAsync())
                        {
                            while (await readerInstalled.ReadAsync())
                                installedProbes.Add(readerInstalled.GetString(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading installed probes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task LoadProbesForPartAsync(string partNo)
        {
            if (string.IsNullOrEmpty(partNo)) return;

            bool networkAvailable = IsNetworkAvailable();
            var loadedProbes = new List<ProbeViewModel>();

            try
            {
                using var con = new SqlConnection(connectionString);
                {
                    await con.OpenAsync();

                    if (!networkAvailable || _orbModules == null)
                    {
                        string queryAll = @"
                            SELECT SrNo, Para_No, Parameter
                            FROM PartConfig
                            WHERE Para_No = @PartNo AND ProbeStatus = 'Probe'
                            ORDER BY SrNo";
                        using (var cmdAll = new SqlCommand(queryAll, con))
                        {
                            cmdAll.Parameters.AddWithValue("@PartNo", partNo);
                            using (var readerAll = await cmdAll.ExecuteReaderAsync())
                            {
                                int counter = 1;
                                while (await readerAll.ReadAsync())
                                {
                                    var probeName = readerAll.GetString(2);
                                    loadedProbes.Add(new ProbeViewModel
                                    {
                                        No = counter++,
                                        Name = probeName,
                                        ProbeName = $"Probe {counter - 1}",
                                        ProbeId = "--",
                                        Stroke = "--",
                                        Status = "Not Connected"
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        var installedProbesForPart = new HashSet<string>();
                        await LoadInstalledProbeIdsAsync(partNo, installedProbesForPart);

                        string query = @"
                            SELECT pc.SrNo, pc.Para_No, pc.Parameter, 
                                ISNULL(pid.ProbeId, '--') AS ProbeId,
                                ISNULL(pid.Stroke, '--') AS Stroke,
                                ISNULL(pid.Status, 'Pending') AS Status
                            FROM PartConfig pc
                            LEFT JOIN ProbeInstallationData pid 
                                ON pc.Para_No = pid.PartNo AND pc.Parameter = pid.Name
                            WHERE pc.Para_No = @PartNo 
                                AND pc.ProbeStatus = 'Probe'
                            ORDER BY pc.SrNo";

                        using (var cmd = new SqlCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@PartNo", partNo);
                            var loadedProbeIds = new HashSet<string>();

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                int counter = 1;
                                while (await reader.ReadAsync())
                                {
                                    var probeName = reader.GetString(2);
                                    var probeId = reader.GetString(3);
                                    var stroke = reader.GetString(4);
                                    var status = reader.GetString(5);

                                    if (probeId != "--" && loadedProbeIds.Contains(probeId))
                                        continue;

                                    if (probeId != "--")
                                        loadedProbeIds.Add(probeId);

                                    if (installedProbesForPart.Contains(probeId))
                                        status = "Installed";
                                    else
                                        status = "Pending";

                                    loadedProbes.Add(new ProbeViewModel
                                    {
                                        No = counter++,
                                        Name = probeName,
                                        ProbeName = $"Probe {counter - 1}",
                                        ProbeId = probeId,
                                        Stroke = stroke,
                                        Status = status
                                    });
                                }
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

        private async void ResetAllProbes_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to reset all saved probe?",
                                         "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
            try
            {
                // 1. Clear saved probe data from DB
                using (var con = new SqlConnection(connectionString))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand("DELETE FROM ProbeInstallationData", con))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // 2. Reset Orbit modules collection in memory
                _orbModules?.ResetTCons();

                // 3. Clear the UI probes collection
                Probes?.Clear();

                // 4. Reset the network controller
                if (_orbNets != null)
                {
                    _orbNet?.Reset();
                    // Optionally, re-initialize or reconnect network here
                }

                MessageBox.Show("All probes have been reset.", "Reset Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                // Optionally, trigger refresh or reload of probes and network status
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting probes, Orbit modules, or network: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private async void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_orbModules == null)
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



                btn.IsEnabled = false;  // Disable button to prevent simultaneous clicks
                try
                {
                    int existingModules = _orbModules.Count;
                    MessageBox.Show("Please move the probe now to be detected. Press ESC to cancel.", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);

                    await Task.Run(() =>
                    {
                        try
                        {
                            _orbModules.NotifyAndAdd($"Module {_orbModules.Count + 1}");
                        }
                        catch (COMException comEx)
                        {
                            // Handle specific COM errors
                            if ((uint)comEx.ErrorCode == 0x800FFFFF) // specific code, adjust if needed
                                throw new InvalidOperationException("Hardware error detected. Please check the device connection.", comEx);
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("Unexpected hardware failure.", ex);
                        }
                    });

                    if (_orbModules.Count == existingModules)
                    {
                        MessageBox.Show("No new probe detected or operation cancelled.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var newModule = _orbModules[_orbModules.Count - 1];
                    dynamic mod = newModule;

                    // Save data to DB and update UI accordingly
                    SaveModelData(SelectedPartNo, probe.Name, mod.ModuleID, mod.Stroke.ToString(), "Installed");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        probe.ProbeId = mod.ModuleID;
                        probe.Stroke = mod.Stroke.ToString();
                        probe.Status = "Installed";
                        probe.ProbeName = $"Probe {probe.No}";
                    });
                }
                catch (InvalidOperationException ioe)
                {
                    MessageBox.Show(ioe.Message, "Hardware Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btn.IsEnabled = true;  // Re-enable button regardless of success/failure
                }
            }
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void ProbeInstallPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_orbServer != null && _orbServer.Connected)
                {
                    _orbServer.Disconnect();
                    _orbServer = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disconnecting Orbit: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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

    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b ? !b : true;
    }
}
