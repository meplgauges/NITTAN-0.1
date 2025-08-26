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

namespace EVMS
{
    /// <summary>
    /// Interaction logic for ProgresBarControl.xaml
    /// </summary>
    public partial class ProgresBarControl : UserControl
    {
        public ProgresBarControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty TitleProperty =
    DependencyProperty.Register("Title", typeof(string), typeof(ProgresBarControl),
        new PropertyMetadata("Probe"));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register("Value", typeof(double), typeof(ProgresBarControl),
         new PropertyMetadata(0.0, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ProgresBarControl;
            if (control != null)
            {
                double newValue = (double)e.NewValue;
                control.Bar.Value = newValue;
                control.BarValue.Text = $"{newValue}%";

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

