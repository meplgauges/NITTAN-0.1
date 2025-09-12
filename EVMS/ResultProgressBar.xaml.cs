using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EVMS
{
    public partial class ResultProgressBar : UserControl
    {
        private readonly DispatcherTimer _timer;
        private readonly Random _random = new Random();

        public ResultProgressBar()
        {
            InitializeComponent();

            // Timer to update values randomly every 1 second
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += UpdateRandomValues;
            _timer.Start();
        }

        private void UpdateRandomValues(object sender, EventArgs e)
        {
            // Generate random values
            double topVal = Math.Round(_random.NextDouble(), 2);          // 0.00 - 1.00
            double leftVal = Math.Round(4.00 + _random.NextDouble() * 0.1, 2); // 4.00 - 4.10
            double rightVal = Math.Round(4.00 + _random.NextDouble() * 0.1, 2);
            double centerVal = Math.Round(_random.NextDouble() * 10, 2);  // 0.00 - 10.00

            // Update UI texts
            TopValueText.Text = topVal.ToString("0.00");
            LeftValueText.Text = leftVal.ToString("0.00");
            RightValueText.Text = rightVal.ToString("0.00");
            CenterValueText.Text = centerVal.ToString("0.00");
            LabelText.Text = "Probe";

            // Update Arc based on topVal (0-1 → 0°-180°)
            double angle = topVal * 180;
            UpdateArc(angle);
        }

        private void UpdateArc(double angle)
        {
            double radius = 50;
            double centerX = 70;
            double centerY = 70;

            // Start point (left side of arc)
            double startX = centerX - radius;
            double startY = centerY;

            // End point (calculated from angle)
            double endX = centerX + radius * Math.Cos(angle * Math.PI / 180);
            double endY = centerY - radius * Math.Sin(angle * Math.PI / 180);

            // Apply arc geometry
            ArcSegment arc = new ArcSegment
            {
                Size = new Size(radius, radius),
                Point = new Point(endX, endY),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = angle > 180
            };

            PathFigure figure = new PathFigure
            {
                StartPoint = new Point(startX, startY),
                Segments = new PathSegmentCollection { arc }
            };

            ValueArc.Data = new PathGeometry(new[] { figure });
        }
    }
}
