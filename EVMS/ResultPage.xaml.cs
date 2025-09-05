using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace EVMS
{
    public partial class ResultPage : UserControl
    {
        private bool useFirstDesign = true; // track current progress bar design
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
        new ValveReadingRow { SerialNumber = 1, GrooveDia = "12.5", STNG = "1", STNU = "2", GroovePositon = "1", SeatRo = "1", HeadDia = "34.2", SeatHeight = "32", DatumToEnd = "64", DatuToGroove = "50", OverLeght = "102" },
        new ValveReadingRow { SerialNumber = 2, GrooveDia = "13", STNG = "3", STNU = "4", GroovePositon = "2", SeatRo = "2", HeadDia = "35", SeatHeight = "33", DatumToEnd = "66", DatuToGroove = "52", OverLeght = "104" },
        new ValveReadingRow { SerialNumber = 3, GrooveDia = "12.5", STNG = "1", STNU = "2", GroovePositon = "1", SeatRo = "1", HeadDia = "34.2", SeatHeight = "32", DatumToEnd = "64", DatuToGroove = "50", OverLeght = "102" },
        new ValveReadingRow { SerialNumber = 4, GrooveDia = "13", STNG = "3", STNU = "4", GroovePositon = "2", SeatRo = "2", HeadDia = "35", SeatHeight = "33", DatumToEnd = "66", DatuToGroove = "52", OverLeght = "104" },
        new ValveReadingRow { SerialNumber = 5, GrooveDia = "12.5", STNG = "1", STNU = "2", GroovePositon = "1", SeatRo = "1", HeadDia = "34.2", SeatHeight = "32", DatumToEnd = "64", DatuToGroove = "50", OverLeght = "102" },
        new ValveReadingRow { SerialNumber = 6, GrooveDia = "13", STNG = "3", STNU = "4", GroovePositon = "2", SeatRo = "2", HeadDia = "35", SeatHeight = "33", DatumToEnd = "66", DatuToGroove = "52", OverLeght = "104" },
         new ValveReadingRow { SerialNumber = 7, GrooveDia = "12.5", STNG = "1", STNU = "2", GroovePositon = "1", SeatRo = "1", HeadDia = "34.2", SeatHeight = "32", DatumToEnd = "64", DatuToGroove = "50", OverLeght = "102" },
        new ValveReadingRow { SerialNumber =8, GrooveDia = "13", STNG = "3", STNU = "4", GroovePositon = "2", SeatRo = "2", HeadDia = "35", SeatHeight = "33", DatumToEnd = "66", DatuToGroove = "52", OverLeght = "104" },
        new ValveReadingRow { SerialNumber = 9, GrooveDia = "12.5", STNG = "1", STNU = "2", GroovePositon = "1", SeatRo = "1", HeadDia = "34.2", SeatHeight = "32", DatumToEnd = "64", DatuToGroove = "50", OverLeght = "102" },
       new ValveReadingRow { SerialNumber = 10, GrooveDia = "12.5", STNG = "1", STNU = "2", GroovePositon = "1", SeatRo = "1", HeadDia = "34.2", SeatHeight = "32", DatumToEnd = "64", DatuToGroove = "50", OverLeght = "102" },

        // ... add more rows

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
                    progressBar = new ResultProgressBar { Margin = new Thickness(5) };
                }
                else
                {
                    progressBar = new ProgresBarControl { Margin = new Thickness(5) };
                }

                ProgressBarContainer.Children.Add(progressBar);
            }
        }

        private void SwitchProgressBar_Click(object sender, RoutedEventArgs e)
        {
            useFirstDesign = !useFirstDesign; // toggle between two designs
            LoadProgressBars();

            // Update button text
            SwitchProgressBarBtn.Content = useFirstDesign
                ? "Switch to Design 2"
                : "Switch to Design 1";
        }
    }

    public class ValveReadingRow
        {
            public int SerialNumber { get; set; }  // Serial number column
            public string OverLeght { get; set; }
            public string DatumToEnd { get; set; }
            public string HeadDia { get; set; }
            public string SeatHeight { get; set; }
            public string GroovePositon { get; set; }
            public string STNG { get; set; }
            public string STNU { get; set; }
            public string GrooveDia { get; set; }
            public string SeatRo { get; set; }
            public string DatuToGroove { get; set; }
        }
    }