using SmartHomeSystem.Backend.Framework;
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
using System.Windows.Shapes;

namespace SmartHomeSystem.Frontend.Forms
{
    /// <summary>
    /// CreateDevice.xaml 的交互逻辑
    /// </summary>
    public partial class CreateDevice : Window
    {
        public CreateDevice()
        {
            InitializeComponent();
            DeviceTypeComboBox.SelectedIndex = 0;
            DurabilityComboBox.SelectedIndex = 1;
        }

        public BaseDevice CreatedDevice { get; private set; }

        public Backend.Framework.Topology.DeviceNode CreatedDeviceNode { get; private set; }

        public double RatedPower { get; private set; }

        public string NodeName { get; private set; }

        public string DeviceType { get; private set; }

        public string Durability { get; private set; }

        public void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证并获取额定功率
                if (!double.TryParse(RatedPowerText.Text.Trim(), out double ratedPower))
                {
                    MessageBox.Show("请输入有效的额定功率数值！", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    RatedPowerText.Focus();
                    return;
                }

                if (ratedPower < 0)
                {
                    MessageBox.Show("额定功率不能为负数！", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    RatedPowerText.Focus();
                    return;
                }

                // 获取节点名
                string nodeName = NodeNameText.Text.Trim();
                if (string.IsNullOrEmpty(nodeName))
                {
                    MessageBox.Show("节点名不能为空！", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    NodeNameText.Focus();
                    return;
                }

                // 获取设备类型
                string deviceType = "";
                if (DeviceTypeComboBox.SelectedItem is ComboBoxItem deviceTypeItem)
                {
                    deviceType = deviceTypeItem.Content.ToString();
                }
                else
                {
                    MessageBox.Show("请选择设备类型！", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    DeviceTypeComboBox.Focus();
                    return;
                }

                // 获取耐用度并转换为性能衰减倍率
                double performanceMultiplier = 1.0;
                if (DurabilityComboBox.SelectedItem is ComboBoxItem durabilityItem)
                {
                    performanceMultiplier = durabilityItem.Content.ToString() switch
                    {
                        "易损" => 5.0,
                        "普通" => 1.0,
                        "难损" => 0.1,
                        _ => 1.0
                    };
                }
                else
                {
                    MessageBox.Show("请选择耐用度！", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    DurabilityComboBox.Focus();
                    return;
                }

                // 根据设备类型创建相应的设备对象
                BaseDevice device = deviceType switch
                {
                    "默认用电器" => new BaseConsumer(nodeName, performanceMultiplier, ratedPower),
                    "默认发电器" => new BaseProducer(nodeName, performanceMultiplier, ratedPower),
                    "默认电池" => new BaseBattery(nodeName, performanceMultiplier, ratedPower),
                    _ => new BaseConsumer(nodeName, performanceMultiplier, ratedPower)
                };

                // 创建设备节点
                var node = new Topology.DeviceNode(device);
                if (FrontContext.selectNode is Topology.LineNode L)
                {
                    L.devices.Add(node);
                    node.parentNode = L;
                }

                // 创建成功后关闭窗口
                this.DialogResult = true;
                this.Close();

                MessageBox.Show($"设备创建成功！\n名称：{nodeName}", "创建成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void CancelButton_Click(Object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}