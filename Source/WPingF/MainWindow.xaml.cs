using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shell;
using System.Xml;
using Visifire.Charts;

namespace WPingF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static bool lRunPing = false;
        static int PingTimeout = 120;
        static string PingAddress = "";

        static ObservableCollection<PingHost> _pingHosts = new ObservableCollection<PingHost>();

        public MainWindow()
        {
            InitializeComponent();

            LoadHosts();

            //ConfigJumpLists();

            HostName.DataContext = _pingHosts;

            VersionText.Content = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString();

        }

        // JumpList support is commented out because I haven't come up with a good use for it yet.
        //private void ConfigJumpLists()
        //{
        //    JumpList jumpList = new JumpList();
        //    jumpList.JumpItemsRemovedByUser += new EventHandler<JumpItemsRemovedEventArgs>(jumpList_JumpItemsRemovedByUser);
        //    jumpList.JumpItemsRejected += new EventHandler<JumpItemsRejectedEventArgs>(jumpList_JumpItemsRejected);

        //    JumpTask jumpTask = new JumpTask()
        //    {
        //        CustomCategory = "Recent",
        //        ApplicationPath = Assembly.GetExecutingAssembly().Location,
        //        Title = "WPingF",
        //        IconResourcePath = Assembly.GetExecutingAssembly().Location
        //    };
        //    jumpList.JumpItems.Add(jumpTask);

        //    JumpPath jumpPath = new JumpPath()
        //    {
        //        Path = Assembly.GetExecutingAssembly().Location, // + " 192.168.16.1",
        //        CustomCategory = "Recent Pings"
        //    };
        //    jumpList.JumpItems.Add(jumpPath);

        //    jumpList.Apply();
        //}

        //void jumpList_JumpItemsRejected(object sender, JumpItemsRejectedEventArgs e)
        //{
        //    int count = e.RejectionReasons.Count();
        //}

        //void jumpList_JumpItemsRemovedByUser(object sender, JumpItemsRemovedEventArgs e)
        //{
        //    int count = e.RemovedItems.Count();
        //}

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (lRunPing == false)
            {
                if (HostName.Text.Length == 0)
                    return;

                if (_pingHosts.Count(x => x.HostName == HostName.Text) == 0)
                {
                    _pingHosts.Add(new PingHost() { HostName = HostName.Text });
                    HostName.SelectedItem = _pingHosts.Last();
                }

                Start.Content = "Stop";

                lRunPing = true;

                PingAddress = HostName.Text;
                if (Timeout.Text == "0" || int.TryParse(Timeout.Text, out PingTimeout) == false)
                    PingTimeout = 120;

                HostName.IsEnabled = false;
                Timeout.IsEnabled = false;

                progressBar1.Maximum = PingTimeout;

                StartPingWorker();
            }
            else
            {
                Start.Content = "Start";

                HostName.IsEnabled = true;
                Timeout.IsEnabled = true;

                lRunPing = false;
            }

        }

        private void PingWorker_Start()
        {
            PerformPing();
        }

        private void StartPingWorker()
        {
            System.Threading.Thread workerThread = new System.Threading.Thread(PingWorker_Start);
            workerThread.Start();
        }

        private void PerformPing()
        {
            if (lRunPing == false)
                return;

            Ping lPingSender = new Ping();
            PingOptions lPingOptions = new PingOptions();

            string lData = "aaaaaaaaaaaaaaaaaaaa";
            byte[] lBuffer = Encoding.ASCII.GetBytes(lData);

            lPingSender.PingCompleted += new PingCompletedEventHandler(PingSender_PingCompleted);

            lPingSender.SendAsync(PingAddress, PingTimeout, lBuffer, lPingOptions);
        }

        private void PingSender_PingCompleted(object sender, PingCompletedEventArgs e)
        {

            PingReply lReply = e.Reply;
            Exception exception = e.Error;

            Results.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                ProcessPingReply(lReply, exception));

            System.Threading.Thread.Sleep(1000);

            PerformPing();
        }

        private Action ProcessPingReply(PingReply lReply, Exception exception)
        {
            return new Action(
                                delegate()
                                {
                                    if (lReply == null)
                                    {
                                        Results.Items.Add(String.Format("{0}\n", exception.Message));
                                        lRunPing = false;
                                        Start.Content = "Start";

                                        HostName.IsEnabled = true;
                                        Timeout.IsEnabled = true;
                                        return;
                                    }

                                    ((PingHost)HostName.SelectedItem).PingReplies.Add(new PingHostReply() { HostPingReply = lReply, ResponseTimestamp = DateTime.Now });

                                    StringBuilder lSB = new StringBuilder();
                                    if (lReply.Status == IPStatus.Success)
                                    {
                                        lSB.AppendLine(String.Format("{0} - Result: {1}, RoundTrip Time: {2}", DateTime.Now.ToLongTimeString(), lReply.Status, lReply.RoundtripTime));
                                        TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
                                        double lPercent = lReply.RoundtripTime / (double)PingTimeout;
                                        TaskbarItemInfo.ProgressValue = lPercent;
                                        progressBar1.Value = lReply.RoundtripTime;
                                    }
                                    else
                                    {
                                        lSB.AppendLine(String.Format("{0} - Result: {1}, RoundTrip Time: {2}", DateTime.Now.ToLongTimeString(), lReply.Status, lReply.RoundtripTime));
                                        TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
                                        TaskbarItemInfo.ProgressValue = 0;
                                        progressBar1.Value = 0;
                                    }
                                    Results.Items.Add(lSB.ToString());

                                    if (Results.Items.Count > 10)
                                    {
                                        Results.Items.RemoveAt(0);
                                    }
                                }
                        );
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveHosts();
        }

        private void LoadHosts()
        {
            if (File.Exists("wpingf.xml") == false)
                return;

            XmlReader xmlReader = XmlReader.Create("wpingf.xml");

            XmlDocument xmlDoc = new XmlDocument();

            xmlDoc.Load(xmlReader);

            XmlNamespaceManager xmlNSMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            xmlNSMgr.AddNamespace("wpingf", "http://www.codereflection.com/WPingF");

            HostName.Items.Clear();

            List<PingHost> pingHostList = new List<PingHost>();

            foreach (XmlNode host in xmlDoc.SelectNodes("/wpingf:root/host", xmlNSMgr))
            {
                pingHostList.Add(new PingHost() { HostName = host.Attributes["name"].Value });
            }

            foreach (PingHost pingHost in pingHostList.OrderBy(x => x.HostName))
            {
                _pingHosts.Add(pingHost);
            }

            xmlReader.Close();
        }

        private void SaveHosts()
        {
            XmlDocument xmlDoc = new XmlDocument();

            XmlNode nRoot = xmlDoc.CreateNode(XmlNodeType.Element, "root", "http://www.codereflection.com/WPingF");
            foreach (PingHost pingHost in _pingHosts)
            {
                XmlElement nHostElement = xmlDoc.CreateElement("host");
                XmlAttribute aHostName = xmlDoc.CreateAttribute("name");
                aHostName.Value = pingHost.HostName;
                nHostElement.Attributes.Append(aHostName);
                nRoot.AppendChild(nHostElement);
            }
            xmlDoc.AppendChild(nRoot);

            XmlWriter xmlWriter = XmlWriter.Create("wpingf.xml");

            xmlDoc.WriteTo(xmlWriter);

            xmlWriter.Close();
        }

        private void btnDeleteHostName_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            _pingHosts.Remove((PingHost)button.CommandParameter);
        }

        private void btnGraphReplies_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            PingHost lPingHost = (PingHost)button.CommandParameter;

            if (lPingHost.PingReplies.Count == 0)
            {
                MessageBox.Show("No replies to graph for this host.");
                return;
            }

            GraphData(lPingHost);
        }

        private void GraphData(PingHost pingHost)
        {
            // Create a new instance of Chart
            Chart chart = new Chart();

            // Set chart properties
            chart.ScrollingEnabled = false;
            chart.View3D = true;

            // Create a new instance of Title
            Title title = new Title();

            // Set title property
            title.Text = "Ping response chart for " + pingHost.HostName;

            // Add title to Titles collection
            chart.Titles.Add(title);

            // Create a new Axis
            Axis axis = new Axis();

            // Set axis properties
            if (pingHost.PingReplies.Count > 60)
            {
                axis.IntervalType = IntervalTypes.Minutes;
            }
            else
            {
                axis.IntervalType = IntervalTypes.Seconds;
            }

            axis.Interval = 1;

            // Add axis to AxesX collection
            chart.AxesX.Add(axis);

            // Create a new instance of DataSeries
            DataSeries dataSeries = new DataSeries();

            // Set DataSeries properties
            dataSeries.RenderAs = RenderAs.Line;
            dataSeries.XValueType = ChartValueTypes.DateTime;

            foreach (PingHostReply pingReply in pingHost.PingReplies)
            {

                // Create a DataPoint
                DataPoint dataPoint;

                // Create a new instance of DataPoint
                dataPoint = new DataPoint();

                // Set XValue for a DataPoint
                dataPoint.XValue = pingReply.ResponseTimestamp;

                // Set YValue for a DataPoint
                dataPoint.YValue = pingReply.HostPingReply.RoundtripTime;

                // Add dataPoint to DataPoints collection
                dataSeries.DataPoints.Add(dataPoint);

            }

            // Add dataSeries to Series collection.
            chart.Series.Add(dataSeries);

            GraphWindow graphWindow = new GraphWindow();

            graphWindow.chart = chart;

            // Add chart to LayoutRoot
            graphWindow.GridRoot.Children.Add(graphWindow.chart);

            graphWindow.Title = chart.Titles[0].Text;

            graphWindow.updateInterface();

            graphWindow.Show();
        }

    }

    public class PingHostReply
    {
        public DateTime ResponseTimestamp { get; set; }
        public PingReply HostPingReply { get; set; }
    }

    public class PingHost
    {
        public string HostName { get; set; }
        public List<PingHostReply> PingReplies = new List<PingHostReply>();
    }
}
