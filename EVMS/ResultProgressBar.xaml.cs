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
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly Random _rnd = new Random();
        private double _value = 0.0; // 0.0 to 1.0

        public ResultProgressBar()
        {
            InitializeComponent();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            UpdateProgress();
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            _value = _rnd.NextDouble(); // Random value 0.0 to 1.0
            UpdateProgress();
        }
        private void UpdateProgress()
        {
            double progress = _value; // 0.0 to 1.0
            double startAngle = 180; // Left
            double sweepAngle = progress * 180; // For semi-circle (full circle: 360)
            double radius = 50;
            double centerX = 70;
            double centerY = 70;
            double endAngle = startAngle + sweepAngle;
            double startRadians = startAngle * Math.PI / 180;
            double endRadians = endAngle * Math.PI / 180;
            Point startPoint = new Point(
                centerX + radius * Math.Cos(startRadians),
                centerY + radius * Math.Sin(startRadians)
            );
            Point endPoint = new Point(
                centerX + radius * Math.Cos(endRadians),
                centerY + radius * Math.Sin(endRadians)
            );
            bool isLargeArc = sweepAngle > 180;
            var pf = new PathFigure { StartPoint = startPoint };
            pf.Segments.Add(new ArcSegment(
                endPoint,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));
            var geom = new PathGeometry();
            geom.Figures.Add(pf);
            ValueArc.Data = geom;
            // Change the ellipse border color based on value
            if (progress < 0.5)
                OuterRing.Stroke = new SolidColorBrush(Colors.Red);
            else
                OuterRing.Stroke = new SolidColorBrush(Colors.Green);
            // Update the value text
            ValueText.Text = $"{progress * 100:F0}";
        }
    }
}
