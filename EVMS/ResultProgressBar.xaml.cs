using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace EVMS
{
    public partial class ResultProgressBar : UserControl
    {
        // Dependency Properties
        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(double),
                typeof(ResultProgressBar), new PropertyMetadata(0.0, OnMinMaxChanged));
        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double),
                typeof(ResultProgressBar), new PropertyMetadata(1.0, OnMinMaxChanged));
        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public static readonly DependencyProperty MeanValueProperty =
            DependencyProperty.Register(nameof(MeanValue), typeof(double),
                typeof(ResultProgressBar), new PropertyMetadata(0.5, OnMeanValueChanged));
        public double MeanValue
        {
            get => (double)GetValue(MeanValueProperty);
            set => SetValue(MeanValueProperty, value);
        }

        public static readonly DependencyProperty ParameterNameProperty =
            DependencyProperty.Register(nameof(ParameterName), typeof(string),
                typeof(ResultProgressBar), new PropertyMetadata(string.Empty));
        public string ParameterName
        {
            get => (string)GetValue(ParameterNameProperty);
            set => SetValue(ParameterNameProperty, value);
        }

        public static readonly DependencyProperty GreenLowThresholdProperty =
            DependencyProperty.Register(nameof(GreenLowThreshold), typeof(double),
                typeof(ResultProgressBar), new PropertyMetadata(double.NaN));
        public double GreenLowThreshold
        {
            get => (double)GetValue(GreenLowThresholdProperty);
            set => SetValue(GreenLowThresholdProperty, value);
        }

        public static readonly DependencyProperty GreenHighThresholdProperty =
            DependencyProperty.Register(nameof(GreenHighThreshold), typeof(double),
                typeof(ResultProgressBar), new PropertyMetadata(double.NaN));
        public double GreenHighThreshold
        {
            get => (double)GetValue(GreenHighThresholdProperty);
            set => SetValue(GreenHighThresholdProperty, value);
        }

        // Geometry
        private double cx, cy, radius;

        public ResultProgressBar()
        {
            InitializeComponent();
            Loaded += ResultProgressBar_Loaded;
            SizeChanged += ResultProgressBar_SizeChanged;
        }

        private void ResultProgressBar_Loaded(object sender, RoutedEventArgs e)
        {
            ComputeGeometry();
            Redraw();
        }

        private void ResultProgressBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ComputeGeometry();
            Redraw();
        }

        private static void OnMinMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ResultProgressBar ctrl)
                ctrl.Redraw();
        }

        private static void OnMeanValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ResultProgressBar ctrl)
                ctrl.UpdateValue((double)e.NewValue);
        }

        private void ComputeGeometry()
        {
            double w = GaugeCanvas.ActualWidth;
            double h = GaugeCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            cx = w / 2.0;
            cy = h * 0.70;
            radius = Math.Min(w, h) * 0.45; // increased radius factor for bigger gauge
        }


        private void Redraw()
        {
            ArcsLayer.Children.Clear();
            DrawArc(MinValue, MaxValue, Brushes.LightGray); // default gray
            DrawTicksAndLabels();
            PositionStaticElements();
        }

        private void DrawArc(double fromVal, double toVal, Brush brush)
        {
            double startTheta = ValueToTheta(fromVal);
            double endTheta = ValueToTheta(toVal);

            Point startPt = PolarToCartesian(cx, cy, radius, startTheta);
            Point endPt = PolarToCartesian(cx, cy, radius, endTheta);

            var fig = new PathFigure { StartPoint = startPt };
            var arcSeg = new ArcSegment
            {
                Point = endPt,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = true
            };
            fig.Segments.Add(arcSeg);

            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            var path = new Path
            {
                Data = geo,
                Stroke = brush,
                StrokeThickness = radius * 0.12,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            ArcsLayer.Children.Add(path);
        }
        private void DrawTicksAndLabels()
        {
            TicksLayer.Children.Clear();
            if (MaxValue <= MinValue) return;

            int majorDivisions = 6;  // Number of major divisions
            int minorDivisions = 4;  // Minor ticks per major division
            int totalTicks = (majorDivisions - 1) * minorDivisions + majorDivisions;
            double step = (MaxValue - MinValue) / (totalTicks - 1);
            double arcThickness = radius * 0.18;
            double tickStartR = radius - (arcThickness / 2.0) - 8;
            double tickEndR_major = tickStartR - radius * 0.08;
            double tickEndR_minor = tickStartR - radius * 0.04;

            int midMajorTickIndex = (totalTicks - 1) / 2;

            for (int i = 0; i < totalTicks; i++)
            {
                double val = (i == 0) ? MinValue : (i == totalTicks - 1) ? MaxValue : MinValue + i * step;
                double theta = ValueToTheta(val);
                double rad = theta * Math.PI / 180.0;
                bool isMajor = (i % minorDivisions == 0) || i == 0 || i == totalTicks - 1;

                Brush tickBrush = Brushes.Black;
                if (i == 0) tickBrush = Brushes.Blue;
                else if (i == midMajorTickIndex) tickBrush = Brushes.Green;
                else if (i == totalTicks - 1) tickBrush = Brushes.Red;

                var tick = new Line
                {
                    X1 = cx + tickStartR * Math.Cos(rad),
                    Y1 = cy - tickStartR * Math.Sin(rad),
                    X2 = cx + (isMajor ? tickEndR_major : tickEndR_minor) * Math.Cos(rad),
                    Y2 = cy - (isMajor ? tickEndR_major : tickEndR_minor) * Math.Sin(rad),
                    Stroke = tickBrush,
                    StrokeThickness = isMajor ? 2 : 1
                };
                TicksLayer.Children.Add(tick);

                if (isMajor)
                {
                    double fontSize = (i == 0 || i == midMajorTickIndex || i == totalTicks - 1) ? radius * 0.07 : radius * 0.05;
                    // Ensure font size is at least 8
                    fontSize = Math.Max(fontSize, 13);
                    var tb = new TextBlock
                    {
                        FontSize = fontSize,
                        FontWeight = FontWeights.Bold,
                        Foreground = tickBrush,
                        Text = val.ToString("0.000")
                    };



                    double labelRadius = tickEndR_major - radius * 0.06;
                    double lx = cx + labelRadius * Math.Cos(rad);
                    double ly = cy - labelRadius * Math.Sin(rad);

                    // Optional: Adjust vertical label position to reduce overlap, e.g., push mid label slightly up/down
                    if (i == midMajorTickIndex) ly -= radius * 0.06;
                    else if (i == 0 || i == totalTicks - 1) ly += radius * 0.04;

                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(tb, lx - tb.DesiredSize.Width / 2);
                    Canvas.SetTop(tb, ly - tb.DesiredSize.Height / 2);
                    TicksLayer.Children.Add(tb);
                }
            }
        }







        private void PositionStaticElements()
        {
            if (radius <= 0) return;
            double needleLength = radius * 0.75;
            Needle.X1 = cx;
            Needle.Y1 = cy;
            Needle.X2 = cx;
            Needle.Y2 = cy - needleLength;
            NeedleRotate.CenterX = cx;
            NeedleRotate.CenterY = cy;

            double hubSize = radius * 0.35;  // bigger hub circle size
            HubContainer.Width = hubSize;
            HubContainer.Height = hubSize;
            Canvas.SetLeft(HubContainer, cx - hubSize / 2);
            Canvas.SetTop(HubContainer, cy - hubSize / 2);
            Canvas.SetLeft(CenterValueText, cx - hubSize * 0.65);
            Canvas.SetTop(CenterValueText, cy - hubSize * 0.45);
            LabelID.Text = ParameterName;
            Canvas.SetLeft(LabelID, cx - hubSize * 0.85);
            Canvas.SetTop(LabelID, cy + hubSize * 0.7);
        }

        public void UpdateValue(double value)
        {
            if (MaxValue <= MinValue) return;

            value = Math.Max(MinValue, Math.Min(MaxValue, value));
            CenterValueText.Text = value.ToString("0.000");

            // Color check
            Brush arcBrush = Brushes.Red;
            if (!double.IsNaN(GreenLowThreshold) && !double.IsNaN(GreenHighThreshold))
            {
                if (value >= GreenLowThreshold && value <= GreenHighThreshold)
                    arcBrush = Brushes.Green;
            }
            ArcsLayer.Children.Clear();
            DrawArc(MinValue, MaxValue, arcBrush);

            // Animate needle
            double center = (MinValue + MaxValue) / 2;
            double halfSpan = (MaxValue - MinValue) / 2;
            double normalized = (value - center) / halfSpan;
            double targetAngle = normalized * 90;

            var anim = new DoubleAnimation
            {
                To = targetAngle,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            NeedleRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
        }

        // Helpers
        private double ValueToTheta(double value)
        {
            return 180.0 - ((value - MinValue) / (MaxValue - MinValue)) * 180.0;
        }

        private static Point PolarToCartesian(double cx, double cy, double r, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new Point(cx + r * Math.Cos(rad), cy - r * Math.Sin(rad));
        }
    }
}
