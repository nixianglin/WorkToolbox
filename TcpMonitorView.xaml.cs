using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WorkToolbox
{
    public partial class TcpMonitorView : UserControl
    {
        // 使用 ObservableCollection 自动更新 UI 表格
        public ObservableCollection<MonitorModel> MonitorItems { get; set; } = new ObservableCollection<MonitorModel>();
        private DispatcherTimer _timer;

        public TcpMonitorView()
        {
            InitializeComponent();
            MonitorDataGrid.ItemsSource = MonitorItems;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += async (s, e) => await RunMonitoring();
            _timer.Start();
        }

        private void AddTarget_Click(object sender, RoutedEventArgs e)
        {
            string input = TargetIpInput.Text.Trim();
            if (!input.Contains(":")) return;

            if (!MonitorItems.Any(i => i.Target == input))
            {
                MonitorItems.Add(new MonitorModel { Target = input });
                MonitorStatus.Visibility = Visibility.Visible;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e) => MonitorItems.Clear();

        private async Task RunMonitoring()
        {
            foreach (var item in MonitorItems)
            {
                await Task.Run(async () =>
                {
                    string[] parts = item.Target.Split(':');
                    string ip = parts[0];
                    int port = int.Parse(parts[1]);

                    // 1. 探测 PING (丢包率计算)
                    Ping pingSender = new Ping();
                    item.SentCount++;
                    try
                    {
                        PingReply reply = pingSender.Send(ip, 500); // 500ms 超时
                        if (reply.Status == IPStatus.Success)
                        {
                            item.Latency = reply.RoundtripTime.ToString();
                        }
                        else
                        {
                            item.LostCount++;
                            item.Latency = "Timeout";
                        }
                    }
                    catch { item.LostCount++; item.Latency = "Error"; }

                    // 2. 探测 TCP 端口
                    using (TcpClient client = new TcpClient())
                    {
                        try
                        {
                            var task = client.ConnectAsync(ip, port);
                            if (await Task.WhenAny(task, Task.Delay(500)) == task && client.Connected)
                            {
                                item.PortStatusText = "Open";
                                item.PortStatusColor = Brushes.LimeGreen;
                            }
                            else
                            {
                                item.PortStatusText = "Closed";
                                item.PortStatusColor = Brushes.Red;
                            }
                        }
                        catch
                        {
                            item.PortStatusText = "Refused";
                            item.PortStatusColor = Brushes.Red;
                        }
                    }
                });
            }
            // 强制 UI 刷新表格内部数据（因为 Model 属性改变了）
            MonitorDataGrid.Items.Refresh();
        }
    }

public class MonitorModel : INotifyPropertyChanged
    {
        private string _latency = "0";
        private string _portStatusText = "Testing";
        private Brush _portStatusColor = Brushes.Gray;
        private int _sentCount;
        private int _lostCount;

        public string Target { get; set; } = "";

        public string Latency { get => _latency; set => SetField(ref _latency, value); }
        public string PortStatusText { get => _portStatusText; set => SetField(ref _portStatusText, value); }
        public Brush PortStatusColor { get => _portStatusColor; set => SetField(ref _portStatusColor, value); }

        public int SentCount { get => _sentCount; set { SetField(ref _sentCount, value); OnPropertyChanged(nameof(LossRate)); OnPropertyChanged(nameof(LossRateText)); OnPropertyChanged(nameof(StatDetail)); OnPropertyChanged(nameof(LossColor)); } }
        public int LostCount { get => _lostCount; set { SetField(ref _lostCount, value); OnPropertyChanged(nameof(LossRate)); OnPropertyChanged(nameof(LossRateText)); OnPropertyChanged(nameof(StatDetail)); OnPropertyChanged(nameof(LossColor)); } }

        // 只读计算属性
        public double LossRate => SentCount == 0 ? 0 : (double)LostCount / SentCount * 100;
        public string LossRateText => $"{Math.Round(LossRate, 1)}%";
        public string StatDetail => $"发:{SentCount} 丢:{LostCount}";
        public Brush LossColor => LossRate > 20 ? Brushes.Red : (LossRate > 5 ? Brushes.Orange : Brushes.LimeGreen);

        // 标准接口实现
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}