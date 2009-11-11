using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Visifire.Charts;

namespace WPingF
{
    /// <summary>
    /// Interaction logic for GraphWindow.xaml
    /// </summary>
    public partial class GraphWindow : Window
    {
        public Chart chart { get; set; }

        public GraphWindow()
        {
            InitializeComponent();



            Array intervals = System.Enum.GetValues(typeof(IntervalTypes));

            IntervalType.Items.Clear();

            foreach (var item in intervals)
            {
                IntervalType.Items.Add(item);
            }
        }

        public void updateInterface()
        {
            if (chart == null)
                return;

            Interval.Text = chart.AxesX[0].Interval.ToString();
            IntervalType.SelectedItem = chart.AxesX[0].IntervalType;
        }

        private void updateChart()
        {
            if (IntervalType.SelectedValue != null)
            {
                chart.AxesX[0].IntervalType = (IntervalTypes)System.Enum.Parse(typeof(IntervalTypes), IntervalType.SelectedValue.ToString());
            }
            double newInterval;
            double.TryParse(Interval.Text, out newInterval);
            chart.AxesX[0].Interval = newInterval;
        }

        private void IntervalType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (chart == null)
                return;

            updateChart();
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            updateChart();
        }
    }
}
