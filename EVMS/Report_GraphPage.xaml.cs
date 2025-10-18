using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace EVMS
{
    public partial class Report_GraphPage : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<ISeries> Series { get; set; }

        public List<string> Labels { get; set; }

        private DateTime? _selectedDate;
        public DateTime? SelectedDate
        {
            get => _selectedDate;
            set { _selectedDate = value; OnPropertyChanged(nameof(SelectedDate)); OnPropertyChanged(nameof(SelectedDateTime)); }
        }

        private TimeSpan? _selectedTime;
        public TimeSpan? SelectedTime
        {
            get => _selectedTime;
            set { _selectedTime = value; OnPropertyChanged(nameof(SelectedTime)); OnPropertyChanged(nameof(SelectedDateTime)); }
        }

        public DateTime? SelectedDateTime
        {
            get
            {
                if (SelectedDate == null || SelectedTime == null) return null;
                return SelectedDate.Value.Date + SelectedTime.Value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Report_GraphPage()
        {
            InitializeComponent();

            try
            {
                Labels = new List<string>();
                for (int i = 1; i <= 17; i++) Labels.Add(i.ToString());

                SelectedDate = DateTime.Today;
                SelectedTime = DateTime.Now.TimeOfDay;

                Series = new ObservableCollection<ISeries>
                {
                    new LineSeries<double>
                    {
                        Values = new double[] {4,6,5,7,3,4,6,5,7,3,4,6,5,7,3,4,6}
                    }
                };

                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load graph page. Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
