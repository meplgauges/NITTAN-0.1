using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace EVMS
{
    public partial class ResultPage : UserControl
    {
        private bool useFirstDesign = true; // Track current progress bar design
        private List<ValveReadingRow> valveDataRows;

        public ResultPage()
        {
            InitializeComponent();

            // Set valve dimension text boxes with sample values
            GrooveDiaBox.Text = "12.5";
            StemDiaBox.Text = "5.807";
            HeadDiaBox.Text = "34.2";
            SeatHeightBox.Text = "32";
            DatumToEndBox.Text = "64";
            DatumToGrooveBox.Text = "50";
            OverallLengthBox.Text = "102";

            // Prepare dummy data for DataGrid
            valveDataRows = new List<ValveReadingRow>
            {
                new ValveReadingRow { SerialNumber = 1, GrooveDia = 12.5, STNG = 1, STNU = 2, GroovePositon = 1, SeatRo = 1, HeadDia = 34.2, SeatHeight = 32, DatumToEnd = 64, DatuToGroove = 50, OverLeght = 102 },
                new ValveReadingRow { SerialNumber = 2, GrooveDia = 13,   STNG = 3, STNU = 4, GroovePositon = 2, SeatRo = 2, HeadDia = 35,   SeatHeight = 33, DatumToEnd = 66, DatuToGroove = 52, OverLeght = 104 },
                new ValveReadingRow { SerialNumber = 3, GrooveDia = 12.7, STNG = 2, STNU = 3, GroovePositon = 1, SeatRo = 1.5, HeadDia = 34.5, SeatHeight = 32.5, DatumToEnd = 65, DatuToGroove = 51, OverLeght = 103 },
                new ValveReadingRow { SerialNumber = 4, GrooveDia = 13.2, STNG = 4, STNU = 5, GroovePositon = 2, SeatRo = 2.5, HeadDia = 35.2, SeatHeight = 33.5, DatumToEnd = 67, DatuToGroove = 53, OverLeght = 105 },
                 new ValveReadingRow { SerialNumber = 3, GrooveDia = 12.7, STNG = 2, STNU = 3, GroovePositon = 1, SeatRo = 1.5, HeadDia = 34.5, SeatHeight = 32.5, DatumToEnd = 65, DatuToGroove = 51, OverLeght = 103 },
                new ValveReadingRow { SerialNumber = 4, GrooveDia = 13.2, STNG = 4, STNU = 5, GroovePositon = 2, SeatRo = 2.5, HeadDia = 35.2, SeatHeight = 33.5, DatumToEnd = 67, DatuToGroove = 53, OverLeght = 105 },
                 new ValveReadingRow { SerialNumber = 3, GrooveDia = 12.7, STNG = 2, STNU = 3, GroovePositon = 1, SeatRo = 1.5, HeadDia = 34.5, SeatHeight = 32.5, DatumToEnd = 65, DatuToGroove = 51, OverLeght = 103 },
                new ValveReadingRow { SerialNumber = 4, GrooveDia = 13.2, STNG = 4, STNU = 5, GroovePositon = 2, SeatRo = 2.5, HeadDia = 35.2, SeatHeight = 33.5, DatumToEnd = 67, DatuToGroove = 53, OverLeght = 105 },
                 new ValveReadingRow { SerialNumber = 4, GrooveDia = 13.2, STNG = 4, STNU = 5, GroovePositon = 2, SeatRo = 2.5, HeadDia = 35.2, SeatHeight = 33.5, DatumToEnd = 67, DatuToGroove = 53, OverLeght = 105 },
                new ValveReadingRow { SerialNumber = 1, GrooveDia = 12.5, STNG = 1, STNU = 2, GroovePositon = 1, SeatRo = 1, HeadDia = 34.2, SeatHeight = 32, DatumToEnd = 64, DatuToGroove = 50, OverLeght = 102 },
                new ValveReadingRow { SerialNumber = 2, GrooveDia = 13,   STNG = 3, STNU = 4, GroovePositon = 2, SeatRo = 2, HeadDia = 35,   SeatHeight = 33, DatumToEnd = 66, DatuToGroove = 52, OverLeght = 104 },
                new ValveReadingRow { SerialNumber = 3, GrooveDia = 12.7, STNG = 2, STNU = 3, GroovePositon = 1, SeatRo = 1.5, HeadDia = 34.5, SeatHeight = 32.5, DatumToEnd = 65, DatuToGroove = 51, OverLeght = 103 },
                new ValveReadingRow { SerialNumber = 4, GrooveDia = 13.2, STNG = 4, STNU = 5, GroovePositon = 2, SeatRo = 2.5, HeadDia = 35.2, SeatHeight = 33.5, DatumToEnd = 67, DatuToGroove = 53, OverLeght = 105 },
                 new ValveReadingRow { SerialNumber = 3, GrooveDia = 12.7, STNG = 2, STNU = 3, GroovePositon = 1, SeatRo = 1.5, HeadDia = 34.5, SeatHeight = 32.5, DatumToEnd = 65, DatuToGroove = 51, OverLeght = 103 },
                new ValveReadingRow { SerialNumber = 4, GrooveDia = 13.2, STNG = 4, STNU = 5, GroovePositon = 2, SeatRo = 2.5, HeadDia = 35.2, SeatHeight = 33.5, DatumToEnd = 67, DatuToGroove = 53, OverLeght = 105 },



            };

            ValveReadingsGrid.ItemsSource = valveDataRows;

            // Load first design progress bars
            LoadProgressBars();

            // Set initial button text
            SwitchProgressBarBtn.Content = useFirstDesign
                ? "Switch to Design 2"
                : "Switch to Design 1";
        }

        private void LoadProgressBars()
        {
            ProgressBarContainer.Children.Clear();

            foreach (var row in valveDataRows)
            {
                UserControl progressBar;

                if (useFirstDesign)
                {
                    // First design
                    var pb = new ResultProgressBar { Margin = new Thickness(5) };
//pb.Value = row.HeadDia; // Set via custom property
                    progressBar = pb;
                }
                else
                {
                    // Second design
                    var pb = new ProgresBarControl { Margin = new Thickness(5) };
                    pb.Value = row.SeatHeight; // Set via custom property
                    progressBar = pb;
                }

                ProgressBarContainer.Children.Add(progressBar);
            }
        }

        private void SwitchProgressBar_Click(object sender, RoutedEventArgs e)
        {
            useFirstDesign = !useFirstDesign; // Toggle between two designs
            LoadProgressBars();

            // Update button text
            SwitchProgressBarBtn.Content = useFirstDesign
                ? "Switch to Design 2"
                : "Switch to Design 1";
        }
    }

    // Data model for valve readings
    public class ValveReadingRow
    {
        public int SerialNumber { get; set; }  // Serial number column
        public double OverLeght { get; set; }
        public double DatumToEnd { get; set; }
        public double HeadDia { get; set; }
        public double SeatHeight { get; set; }
        public double GroovePositon { get; set; }
        public double STNG { get; set; }
        public double STNU { get; set; }
        public double GrooveDia { get; set; }
        public double SeatRo { get; set; }
        public double DatuToGroove { get; set; }
    }
}
