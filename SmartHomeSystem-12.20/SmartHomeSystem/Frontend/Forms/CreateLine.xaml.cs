using SmartHomeSystem.Connection;
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
    /// CreateLine.xaml 的交互逻辑
    /// </summary>
    public partial class CreateLine : Window
    {
        public CreateLine()
        {
            InitializeComponent();
        }

        public void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string _name = NodeNameText.Text.Trim();
                if (!double.TryParse(MaxPowerText.Text.Trim(), out double _maxPower))
                {
                    MessageBox.Show("请输入有效的最大功率数值！", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_maxPower < 0)
                {
                    MessageBox.Show("最大功率不能为负数！", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var node = new Backend.Framework.Topology.LineNode()
                {
                    maxPower = _maxPower,
                    name = _name,
                };
                if (FrontContext.selectNode is Backend.Framework.Topology.LineNode L)
                {
                    node.parentNode = L;
                    L.childrenNode.Add(node);
                }
                this.DialogResult = true;
                this.Close();
                MainViewModel._instance.RefreshEntireTree();
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