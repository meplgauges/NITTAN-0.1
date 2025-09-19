using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EVMS
{
    public partial class ProgresBarControl : UserControl
    {
        public ProgresBarControl()
        {
            InitializeComponent();
        }

        // Corrected 'Title' DependencyProperty registration (fixing typo "Titl" -> "Title")
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(ProgresBarControl),
                new PropertyMetadata("Probe"));

        public double Min
        {
            get => (double)GetValue(MinProperty);
            set => SetValue(MinProperty, value);
        }
        public static readonly DependencyProperty MinProperty =
            DependencyProperty.Register(nameof(Min), typeof(double), typeof(ProgresBarControl), new PropertyMetadata(0.0));

        public double Mean
        {
            get => (double)GetValue(MeanProperty);
            set => SetValue(MeanProperty, value);
        }
        public static readonly DependencyProperty MeanProperty =
            DependencyProperty.Register(nameof(Mean), typeof(double), typeof(ProgresBarControl), new PropertyMetadata(0.0));

        public double Max
        {
            get => (double)GetValue(MaxProperty);
            set => SetValue(MaxProperty, value);
        }
        public static readonly DependencyProperty MaxProperty =
            DependencyProperty.Register(nameof(Max), typeof(double), typeof(ProgresBarControl), new PropertyMetadata(100.0));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(ProgresBarControl),
                new PropertyMetadata(0.0, OnValueChanged));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgresBarControl control)
            {
                double newValue = (double)e.NewValue;
                control.Bar.Value = newValue;
                control.BarValue.Text = $"{newValue:F1}%"; // Format with one decimal place

                // Change color based on value thresholds
                if (newValue < 20 || newValue > 90)
                {
                    // Red color brush
                    control.Bar.Foreground = new SolidColorBrush(Colors.Red);
                }
                else
                {
                    // Default gradient brush
                    var gradientBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 1),
                        EndPoint = new Point(0, 0)
                    };
                    gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x4C, 0xAF, 0x50), 0)); // #4CAF50 green
                    gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x21, 0x96, 0xF3), 1)); // #2196F3 blue
                    control.Bar.Foreground = gradientBrush;
                }
            }
        }
    }
}
