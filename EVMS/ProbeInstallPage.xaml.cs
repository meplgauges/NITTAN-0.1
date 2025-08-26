using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace EVMS
{
    public partial class ProbeInstallPage : UserControl, INotifyPropertyChanged
    {
        private string _selectedModel;

        public ObservableCollection<string> Models { get; set; }
        public ObservableCollection<Probe> Probes { get; set; }

        public string SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (_selectedModel != value)
                {
                    _selectedModel = value;
                    OnPropertyChanged();
                    LoadProbesForModel(_selectedModel);
                }
            }
        }

        public ProbeInstallPage()
        {
            InitializeComponent();

            // Initialize collections
            Models = new ObservableCollection<string> { "Model A", "Model B", "Model C" };
            Probes = new ObservableCollection<Probe>();

            // Set default model
            if (Models.Count > 0) SelectedModel = Models[0];

            DataContext = this; // Bind to itself
        }

        private void LoadProbesForModel(string model)
        {
            Probes.Clear();

            if (model == "Model A")
            {
                Probes.Add(new Probe { No = 1, Name = "Sensor A", ProbeName = "OverLength", ProbeId = "PR-101", Stroke = "25 mm", Status = "Pending" });
                Probes.Add(new Probe { No = 2, Name = "Sensor B", ProbeName = "HeadDia", ProbeId = "PR-102", Stroke = "30 mm", Status = "Installed" });
            }
            else if (model == "Model B")
            {
                Probes.Add(new Probe { No = 1, Name = "Sensor X", ProbeName = "LengthChk", ProbeId = "PR-201", Stroke = "28 mm", Status = "Pending" });
                Probes.Add(new Probe { No = 2, Name = "Sensor Y", ProbeName = "WidthChk", ProbeId = "PR-202", Stroke = "33 mm", Status = "Pending" });
            }
        }

        // Probe class
        public class Probe
        {
            public int No { get; set; }
            public string Name { get; set; }
            public string ProbeName { get; set; }
            public string ProbeId { get; set; }
            public string Stroke { get; set; }
            public string Status { get; set; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
