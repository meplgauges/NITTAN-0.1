using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EVMS
{
    public partial class ResultProgressBar : UserControl
    {
        // configuration
        private const double MinValue = -6.0;
        private const double MaxValue = 6.0;
        private const double MinorStep = 0.2;
        private const double MajorStep = 1.0;

        // computed geometry (in canvas coordinates)
        private double cx, cy, radius;

        private readonly Random _random = new Random();
        private readonly DispatcherTimer _timer;

        public ResultProgressBar()
        {
            InitializeComponent();

            Loaded += ResultProgressBar_Loaded;
            SizeChanged += ResultProgressBar_SizeChanged;

            // simple demo timer that moves the needle randomly
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
        }

        private void ResultProgressBar_Loaded(object sender, RoutedEventArgs e)
        {
            ComputeGeometry();
            Redraw();
            _timer.Start();
        }


        private void ResultProgressBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // If logical canvas size changed (unlikely since it's fixed in XAML),
            // recompute geometry and redraw so things remain aligned.
            ComputeGeometry();
            Redraw();
        }

        private void ComputeGeometry()
        {
            // Use the Canvas logical Width/Height defined in XAML as base units (Viewbox scales later)
            double w = GaugeCanvas.Width;
            double h = GaugeCanvas.Height;

            cx = w / 2.0;                  // center X in canvas coords
            cy = h * 0.70;                 // center Y pushed down so arc sits above
            radius = Math.Min(w, h) * 0.4; // radius relative to canvas
        }

        private void Redraw()
        {
            DrawArcs();
            DrawTicksAndLabels();
            PositionStaticElements();
        }

        private void DrawArcs()
        {
            ArcsLayer.Children.Clear();

            double arcThickness = radius * 0.12;

            void AddArc(double fromValue, double toValue, Brush brush)
            {
                double startTheta = ValueToTheta(fromValue);
                double endTheta = ValueToTheta(toValue);

                Point start = PolarToCartesian(cx, cy, radius, startTheta);
                Point end = PolarToCartesian(cx, cy, radius, endTheta);

                bool isLarge = Math.Abs(NormalizeAngle(endTheta - startTheta)) > 180;

                var fig = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
                var seg = new ArcSegment
                {
                    Point = end,
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = isLarge
                };
                fig.Segments.Add(seg);

                var geom = new PathGeometry();
                geom.Figures.Add(fig);

                var path = new Path
                {
                    Data = geom,
                    Stroke = brush,
                    StrokeThickness = arcThickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    SnapsToDevicePixels = true
                };

                ArcsLayer.Children.Add(path);
            }

            // yellow: -6 to -4, green: -4 to 4, red: 4 to 6
            AddArc(-6.0, -2.0, Brushes.Orange);
            AddArc(-2.0, 2.0, Brushes.Green);
            AddArc(2, 6.0, Brushes.Red);
        }

        private void DrawTicksAndLabels()
        {
            TicksLayer.Children.Clear();

            double arcThickness = radius * 0.12;
            double outerR = radius - (arcThickness / 2.0) - 2.0;
            double majorTickLen = radius * 0.12;
            double minorTickLen = radius * 0.06;

            int minorCount = (int)Math.Round((MaxValue - MinValue) / MinorStep);

            for (int i = 0; i <= minorCount; i++)
            {
                double value = MinValue + i * MinorStep;
                double theta = ValueToTheta(value);
                double rad = theta * Math.PI / 180.0;

                bool isMajor = Math.Abs((value / MajorStep) - Math.Round(value / MajorStep)) < 1e-6;

                double len = isMajor ? majorTickLen : minorTickLen;

                double x1 = cx + outerR * Math.Cos(rad);
                double y1 = cy - outerR * Math.Sin(rad);
                double x2 = cx + (outerR - len) * Math.Cos(rad);
                double y2 = cy - (outerR - len) * Math.Sin(rad);

                var tick = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = Brushes.Black,
                    StrokeThickness = isMajor ? 2.0 : 1.0,
                    Opacity = 0.95
                };
                TicksLayer.Children.Add(tick);
            }

            // major labels (integers)
            for (int i = (int)MinValue; i <= (int)MaxValue; i++)
            {
                double theta = ValueToTheta(i);
                double rad = theta * Math.PI / 180.0;

                double labelR = outerR - majorTickLen - (radius * 0.08);
                double lx = cx + labelR * Math.Cos(rad);
                double ly = cy - labelR * Math.Sin(rad);

                var tb = new TextBlock
                {
                    Text = i.ToString("0.0"),
                    FontSize = Math.Max(10, radius * 0.08),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.Black
                };

                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Size sz = tb.DesiredSize;

                Canvas.SetLeft(tb, lx - sz.Width / 2.0);
                Canvas.SetTop(tb, ly - sz.Height / 2.0);
                TicksLayer.Children.Add(tb);
            }
        }

        private void PositionStaticElements()
        {
            // needle length and position
            double needleLen = radius * 0.75;
            Needle.X1 = cx;
            Needle.Y1 = cy;
            Needle.X2 = cx;
            Needle.Y2 = cy - needleLen;

            // needle rotate center (pivot)
            NeedleRotate.CenterX = cx;
            NeedleRotate.CenterY = cy;

            // hub size proportional to radius
            double hubSize = radius * 0.18;
            if (hubSize < 24) hubSize = 24; // minimum size
            if (hubSize > 60) hubSize = 60; // maximum reasonable size

            HubContainer.Width = hubSize;
            HubContainer.Height = hubSize;

            // center the hub on cx,cy
            Canvas.SetLeft(HubContainer, cx - hubSize / 2.0);
            Canvas.SetTop(HubContainer, cy - hubSize / 2.0);

            // center value font size relative to hub
            CenterValueText.FontSize = Math.Max(8, hubSize * 0.32);

            // place the bottom ID label centered below hub
            LabelID.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var labelSize = LabelID.DesiredSize;
            Canvas.SetLeft(LabelID, cx - labelSize.Width / 2.0);
            Canvas.SetTop(LabelID, cy + hubSize * 0.55);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // produce random demo value
            double value = Math.Round((_random.NextDouble() * (MaxValue - MinValue)) + MinValue, 3);
            UpdateValue(value);
        }

        /// <summary>
        /// Public: update gauge value (-6..6). Animates needle and updates center text.
        /// </summary>
        public void UpdateValue(double value)
        {
            if (value < MinValue) value = MinValue;
            if (value > MaxValue) value = MaxValue;

            CenterValueText.Text = value.ToString("0.000");

            // rotate needle: mapping value -> degrees
            // full span -6..6 -> -90..+90 (one unit = 15 degrees)
            double targetAngle = value * 15.0;

            var anim = new DoubleAnimation
            {
                To = targetAngle,
                Duration = TimeSpan.FromMilliseconds(420),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            NeedleRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
        }

        // Helpers
        private static double ValueToTheta(double value)
        {
            // maps value (-6..6) -> theta in degrees: -6 => 180 (left), 0 => 90 (top), +6 => 0 (right)
            double t = 180.0 - ((value - MinValue) / (MaxValue - MinValue)) * 180.0;
            return t;
        }

        private static double NormalizeAngle(double a)
        {
            while (a > 180) a -= 360;
            while (a <= -180) a += 360;
            return a;
        }

        private static Point PolarToCartesian(double cx, double cy, double r, double thetaDeg)
        {
            double rad = thetaDeg * Math.PI / 180.0;
            double x = cx + r * Math.Cos(rad);
            double y = cy - r * Math.Sin(rad); // screen Y is downwards: subtract
            return new Point(x, y);
        }
    }
}
