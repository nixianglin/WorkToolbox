using System;
using System.Management;
using System.Windows;
using System.Windows.Controls;

namespace WorkToolbox
{
    public partial class NetworkSettingView : UserControl
    {
        public NetworkSettingView()
        {
            InitializeComponent();
            LoadAdapters();
        }

        private void LoadAdapters()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                foreach (ManagementObject obj in searcher.Get())
                {
                    AdapterCombo.Items.Add(obj["Description"].ToString());
                }
                if (AdapterCombo.Items.Count > 0) AdapterCombo.SelectedIndex = 0;
            }
            catch (Exception ex) { StatusText.Text = "网卡加载异常: " + ex.Message; }
        }

        private void ApplyIP_Click(object sender, RoutedEventArgs e) => SetNetwork(IPTextBox.Text, SubnetTextBox.Text, GatewayTextBox.Text);
        private void ResetDHCP_Click(object sender, RoutedEventArgs e) => SetNetwork(null, null, null, true);

        private void SetNetwork(string ip, string mask, string gateway, bool isDHCP = false)
        {
            try
            {
                string? adapterDesc = AdapterCombo.SelectedItem?.ToString();
                using var mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
                using var moc = mc.GetInstances();

                foreach (ManagementObject mo in moc)
                {
                    if (!(bool)mo["IPEnabled"] || mo["Description"].ToString() != adapterDesc) continue;

                    if (isDHCP)
                    {
                        mo.InvokeMethod("EnableDHCP", null);
                        MessageBox.Show("已切换为自动获取 IP");
                    }
                    else
                    {
                        var setIP = mo.GetMethodParameters("EnableStatic");
                        setIP["IPAddress"] = new[] { ip };
                        setIP["SubnetMask"] = new[] { mask };

                        var setGate = mo.GetMethodParameters("SetGateways");
                        setGate["DefaultIPGateway"] = new[] { gateway };
                        setGate["GatewayCostMetric"] = new[] { 1 };

                        mo.InvokeMethod("EnableStatic", setIP, null);
                        mo.InvokeMethod("SetGateways", setGate, null);
                        MessageBox.Show("静态 IP 设置成功");
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("请尝试管理员运行: " + ex.Message); }
        }
        // 当下拉框切换网卡时，自动读取该网卡的当前配置
        private void AdapterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCurrentSettings();
        }

        private void UpdateCurrentSettings()
        {
            try
            {
                string? selectedAdapter = AdapterCombo.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedAdapter)) return;

                using var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE Description = '{selectedAdapter.Replace("\\", "\\\\")}'");

                foreach (ManagementObject mo in searcher.Get())
                {
                    // 读取 IP 地址数组（通常第一个是 IPv4）
                    string[]? addresses = (string[]?)mo["IPAddress"];
                    string[]? subnets = (string[]?)mo["IPSubnet"];
                    string[]? gateways = (string[]?)mo["DefaultIPGateway"];

                    // 填充到前端界面
                    IPTextBox.Text = addresses?.Length > 0 ? addresses[0] : "";
                    SubnetTextBox.Text = subnets?.Length > 0 ? subnets[0] : "";
                    GatewayTextBox.Text = gateways?.Length > 0 ? gateways[0] : "";

                    StatusText.Text = $"当前状态：{((bool)mo["DHCPEnabled"] ? "自动获取 (DHCP)" : "手动配置")}";
                    break;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "读取当前配置失败: " + ex.Message;
            }
        }

    }
}