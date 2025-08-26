using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EVMS
{
    /// <summary>
    /// Interaction logic for ProbeSetup.xaml
    /// </summary>
    public partial class ProbeSetupPage : UserControl
    {
        private DispatcherTimer _timer;
        private Random _random = new Random();

        public ProbeSetupPage()
        {
           InitializeComponent();

            // Load measurement rows (simulate data for now)
            var rows = new List<Row>
            {
                new Row{ Title = "Probe 1",  Value = 4.05, Timestamp = DateTime.Now },
                new Row{ Title = "Probe 2",  Value = 4.07, Timestamp = DateTime.Now },
                new Row{ Title = "Probe 3",  Value = 4.01, Timestamp = DateTime.Now },
                new Row{ Title = "Probe 4",  Value = 4.10, Timestamp = DateTime.Now },
                new Row{ Title = "Probe 5",  Value = 4.03, Timestamp = DateTime.Now },
                new Row{ Title = "Probe 6",  Value = 4.08, Timestamp = DateTime.Now },
                new Row{ Title = "Probe 7",  Value = 4.00, Timestamp = DateTime.Now },
                new Row{ Title = "Probe 8",  Value = 4.06, Timestamp = DateTime.Now },
                new Row{ Title = "Probe 9",  Value = 4.09, Timestamp = DateTime.Now },
                new Row{ Title = "Probe 10", Value = 4.02, Timestamp = DateTime.Now },
            };


            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();


            // Bind to table
            //MeasurementTable.ItemsSource = rows;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            PB1.Value = _random.Next(0, 101); // generates int between 0 and 100 inclusive
            PB2.Value = _random.Next(0, 101);
            PB3.Value = _random.Next(0, 101);
            PB4.Value = _random.Next(0, 101);
            PB5.Value = _random.Next(0, 101);
            PB6.Value = _random.Next(0, 101);
            PB7.Value = _random.Next(0, 101);
            PB8.Value = _random.Next(0, 101);
            PB9.Value = _random.Next(0, 101);
            PB10.Value = _random.Next(0, 101);


        }

        private void ProbeScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var sv = (ScrollViewer)sender;

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);  // Shift+Wheel = horizontal
            else
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);      // Wheel = vertical

            e.Handled = true;
        }


        public class Row
        {
            public string Title { get; set; } = string.Empty;
            public double Value { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}