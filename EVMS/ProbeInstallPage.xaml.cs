using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using OrbitCOM;

namespace EVMS
{
    public partial class ProbeInstallPage : UserControl, INotifyPropertyChanged
    {
        private OrbitServerClass _orbServer;
        private OrbitNetworks _orbNets;
        private OrbitNetwork _orbNet;
        private OrbitModules _orbModules;
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
                    LoadProbesForPart(_selectedPartNo);
                }
            }
        }

        public ProbeInstallPage()
        {
            connectionString = ConfigurationManager.ConnectionStrings["EVMSDb"].ConnectionString;
            InitializeComponent();

            Probes = new ObservableCollection<ProbeViewModel>();
            PartNumbers = new ObservableCollection<string>();
            this.DataContext = this;

            Loaded += ProbeInstallPage_Loaded;
            Unloaded += ProbeInstallPage_Unloaded;  // subscribe to Unloaded event

        }

        private void ProbeInstallPage_Loaded(object sender, RoutedEventArgs e)
        {
            ConnectToOrbit();
            LoadPartNumbers();
            if (PartNumbers.Count > 0)
                SelectedPartNo = PartNumbers[0]; // Select first part by default
        }

        private void ConnectToOrbit()
        {
            try
            {
                _orbServer = new OrbitServerClass();
                if (!_orbServer.Connected)
                {
                    _orbServer.Connect();
                }
                if (_orbServer.Connected)
                {
                    _orbNets = _orbServer.Networks;
                    if (_orbNets != null && _orbNets.Count > 0)
                    {
                        _orbNet = _orbNets[0];
                        _orbModules = _orbNet.Modules;
                        MessageBox.Show($"{_orbNets.Count} Networks Found. Connected to Orbit.",
                            "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No networks found in Orbit.", "Warning",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Failed to connect to Orbit.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while connecting to Orbit: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPartNumbers()
        {
            PartNumbers.Clear();
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = "SELECT DISTINCT Para_No FROM PART_ENTRY ORDER BY Para_No";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PartNumbers.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Part Numbers: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProbesForPart(string partNo)
        {
            Probes.Clear();
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
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
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@PartNo", partNo);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            int counter = 1;
                            while (reader.Read())
                            {
                                var probeName = reader.GetString(2); // Parameter column
                                var probeId = reader.GetString(3);
                                var stroke = reader.GetString(4);
                                var status = reader.GetString(5);

                                // Check Orbit connection to update status
                                if (_orbModules != null)
                                {
                                    bool isConnected = false;
                                    foreach (var moduleObj in _orbModules)
                                    {
                                        dynamic module = moduleObj;
                                        if (module.ModuleID == probeId && module.ModuleName == probeName)
                                        {
                                            isConnected = true;
                                            break;
                                        }
                                    }
                                    if (isConnected)
                                        status = "Installed";
                                    else if (status != "Installed")
                                        status = "Pending";
                                }

                                Probes.Add(new ProbeViewModel
                                {
                                    No = counter,
                                    Name = probeName,
                                    ProbeName = $"Probe {counter}",
                                    ProbeId = probeId,
                                    Stroke = stroke,
                                    Status = status
                                });
                                counter++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading probes for part: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public void SaveModelData(string partNo, string name, string probeId, string stroke, string status)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();

                    // Check if record exists
                    string checkQuery = @"
                        SELECT COUNT(*) FROM ProbeInstallationData 
                        WHERE PartNo = @PartNo AND Name = @Name";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                    {
                        checkCmd.Parameters.AddWithValue("@PartNo", partNo);
                        checkCmd.Parameters.AddWithValue("@Name", name);
                        int count = (int)checkCmd.ExecuteScalar();

                        string query;
                        if (count > 0)
                        {
                            // Update existing record
                            query = @"
                                UPDATE ProbeInstallationData
                                SET ProbeId = @ProbeId, Stroke = @Stroke, Status = @Status
                                WHERE PartNo = @PartNo AND Name = @Name";
                        }
                        else
                        {
                            // Insert new record
                            query = @"
                                INSERT INTO ProbeInstallationData (PartNo, Name, ProbeId, Stroke, Status)
                                VALUES (@PartNo, @Name, @ProbeId, @Stroke, @Status)";
                        }

                        using (SqlCommand cmd = new SqlCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@PartNo", partNo);
                            cmd.Parameters.AddWithValue("@Name", name);
                            cmd.Parameters.AddWithValue("@ProbeId", probeId);
                            cmd.Parameters.AddWithValue("@Stroke", stroke);
                            cmd.Parameters.AddWithValue("@Status", status);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving model data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_orbModules == null)
            {
                MessageBox.Show("Orbit modules are not initialized. Please connect first.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (sender is Button btn && btn.CommandParameter is ProbeViewModel probe)
            {
                try
                {
                    int currentCount = _orbModules.Count;
                    MessageBox.Show("Notifying Module (move probe now). Press ESC to cancel.",
                        "Notification", MessageBoxButton.OK, MessageBoxImage.Information);

                    string name = $"Module {_orbModules.Count + 1}";

                    _orbModules.NotifyAndAdd(name);

                    if (_orbModules.Count == currentCount)
                    {
                        MessageBox.Show("No modules were added. Probe not detected or cancelled.",
                            "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var module = _orbModules[_orbModules.Count - 1];
                    dynamic mod = module;

                    probe.ProbeId = mod.ModuleID;
                    probe.Stroke = mod.Stroke.ToString();
                    probe.Status = "Installed";
                    probe.ProbeName = $"Probe {probe.No}";

                    // Save probe data automatically when installed
                    SaveModelData(SelectedPartNo, probe.Name, probe.ProbeId, probe.Stroke, probe.Status);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error while notifying/adding module: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


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
                // Log or handle disconnect errors if necessary
                MessageBox.Show($"Error disconnecting Orbit: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ProbeViewModel : INotifyPropertyChanged
    {
        private int _no;
        private string _name;
        private string _probeName;
        private string _probeId;
        private string _stroke;
        private string _status;

        public int No
        {
            get => _no;
            set { _no = value; OnPropertyChanged(); }
        }
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        public string ProbeName
        {
            get => _probeName;
            set { _probeName = value; OnPropertyChanged(); }
        }
        public string ProbeId
        {
            get => _probeId;
            set { _probeId = value; OnPropertyChanged(); }
        }
        public string Stroke
        {
            get => _stroke;
            set { _stroke = value; OnPropertyChanged(); }
        }
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


   
    }

}
