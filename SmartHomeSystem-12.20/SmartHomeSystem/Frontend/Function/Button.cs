using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;
using SmartHomeSystem.Backend.Framework;
using SmartHomeSystem.Connection;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static SmartHomeSystem.Backend.Framework.Topology;

namespace SmartHomeSystem.Frontend.Function
{
    public static class Button
    {
        private static bool bigChartOpen = false;
        private static UIElement storage = null;

        public static void ChartDataMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            try
            {
                if (bigChartOpen)
                {
                    FrontContext.CurrentWindow.RightView.Child = null;
                    FrontContext.CurrentWindow.RightView.Child = storage;
                    storage = null;
                    bigChartOpen = false;
                }
                else
                {
                    storage = FrontContext.CurrentWindow.RightView.Child;
                    bigChartOpen = true;
                    var bigChart = new CartesianChart
                    {
                        Series = new SeriesCollection(),
                        LegendLocation = LegendLocation.Right
                    };

                    foreach (var series in FrontContext.CurrentWindow.ChartData.Series)
                    {
                        if (series is LineSeries lineSeries)
                        {
                            bigChart.Series.Add(new LineSeries
                            {
                                Title = lineSeries.Title,
                                Values = lineSeries.Values
                            });
                        }
                    }

                    bigChart.AxisY.Add(new Axis { Title = "总用电量" });
                    bigChart.AxisX.Add(new Axis { Title = "时间" });

                    var grid = new Grid();
                    grid.Children.Add(bigChart);
                    FrontContext.CurrentWindow.RightView.Child = grid;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"图表数据鼠标点击事件失败: {ex.Message}");
            }
        }

        #region 清空树结构

        public static void ClearDevicesOnly()
        {
            ClearNodeDevices(root);
            ClearDeviceContainer();
            ClearStatistics();

            LogManager.Log("系统", "设备数据已清空，拓扑结构保留");
        }

        public static void EmptyButtonClick(object sender, RoutedEventArgs e)
        {
            Time.Pause();
            FrontContext.CurrentWindow.pause.IsChecked = true;
            ClearNode(root);
            ClearStatistics();
            ClearDeviceContainer();
            MainViewModel._instance.RefreshEntireTree();
            LogManager.Log("系统", "拓扑结构已清空");
        }

        private static void ClearDeviceContainer()
        {
            // 释放设备资源
            foreach (var device in allService.Values)
            {
                if (device is IDisposable disposable)
                    disposable.Dispose();
            }
            allService.Clear();
        }

        private static void ClearNode(LineNode node)
        {
            if (node == null) return;

            // 清空设备节点（释放设备资源）
            foreach (var deviceNode in node.devices)
            {
                if (deviceNode.currentDevice != null)
                {
                    // 触发设备的清理逻辑（如果有的话）
                    if (deviceNode.currentDevice is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    deviceNode.currentDevice = null;
                }
                deviceNode.parentNode = null;
            }
            node.devices.Clear();

            // 递归清空子节点
            foreach (var childNode in node.childrenNode)
            {
                ClearNode(childNode);
                childNode.parentNode = null;
            }
            node.childrenNode.Clear();

            // 重置节点状态（如果是根节点则保持基本配置）
            if (node != root)
            {
                node.PowerConsumption = 0;
                node.PowerProduction = 0;
                node.isOverLoad = false;
                node.nodeSwitch = true;
            }
            else
            {
                // 根节点只重置功率数据，保持其他配置
                node.PowerConsumption = 0;
                node.PowerProduction = 0;
                node.isOverLoad = false;
            }
        }

        private static void ClearNodeDevices(LineNode node)
        {
            if (node == null) return;

            // 清空当前节点的设备
            foreach (var deviceNode in node.devices)
            {
                if (deviceNode.currentDevice != null)
                {
                    if (deviceNode.currentDevice is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    deviceNode.currentDevice = null;
                }
                deviceNode.parentNode = null;
            }
            node.devices.Clear();

            // 递归清空子节点的设备
            foreach (var childNode in node.childrenNode)
            {
                ClearNodeDevices(childNode);
            }
        }

        private static void ClearStatistics()
        {
            ConsumptionHistory.Clear();
            cumulativeElectricityConsumption = 0;
            cumulativeElectricityPricing = 0;

            MoneyHistory.Clear();
            MoneyHistory.Add((0, 0)); // 保留初始值
            PriceHistory.Clear();
        }

        #endregion 清空树结构

        public static void ExitButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Frontend.FrontContext.CurrentWindow.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"退出按钮点击事件失败: {ex.Message}");
            }
        }

        public static void LoadButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Time.Pause();
                FrontContext.CurrentWindow.pause.IsChecked = true;
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "智能家居系统文件 (*.sav)|*.sav|所有文件 (*.*)|*.*",
                    Title = "选择要加载的保存文件",
                    InitialDirectory = Environment.CurrentDirectory,
                    CheckFileExists = true,
                    CheckPathExists = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string selectedFileName = openFileDialog.FileName;
                    bool success = Backend.Function.LoadManager.Load(selectedFileName);

                    if (success)
                    {
                        MessageBox.Show($"文件加载成功: {Path.GetFileName(selectedFileName)}", "加载成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        LogManager.Log("系统", $"已加载保存文件: {selectedFileName}");
                    }
                    else
                    {
                        MessageBox.Show("文件加载失败，请检查文件是否完整", "加载失败",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                MainViewModel._instance.RefreshEntireTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载文件时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"加载按钮点击事件失败: {ex.Message}");
            }
        }

        public static void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                var radioButton = sender as RadioButton;
                if (radioButton != null && radioButton.IsChecked == true)
                {
                    switch (radioButton.Name)
                    {
                        case "pause":
                            Backend.Framework.Time.Pause();
                            break;

                        case "speed1":
                            Backend.Framework.Time.SetTimeScale("实时");
                            Backend.Framework.Time.UnPause();
                            break;

                        case "speed2":
                            Backend.Framework.Time.SetTimeScale("低速");
                            Backend.Framework.Time.UnPause();
                            break;

                        case "speed3":
                            Backend.Framework.Time.SetTimeScale("中速");
                            Backend.Framework.Time.UnPause();
                            break;

                        case "speed4":
                            Backend.Framework.Time.SetTimeScale("快速");
                            Backend.Framework.Time.UnPause();
                            break;
                    }

                    Debug.WriteLine($"时间尺度设置为: {radioButton.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"单选按钮检查事件失败: {ex.Message}");
            }
        }

        public static void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Time.Pause();
                FrontContext.CurrentWindow.pause.IsChecked = true;
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "智能家居系统文件 (*.sav)|*.sav|所有文件 (*.*)|*.*",
                    Title = "保存系统状态",
                    InitialDirectory = Environment.CurrentDirectory,
                    DefaultExt = ".sav",
                    FileName = $"smarthome_{DateTime.Now:yyyyMMdd_HHmmss}.sav",
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string selectedFileName = saveFileDialog.FileName;
                    Backend.Function.SaveManager.Save(selectedFileName);

                    MessageBox.Show($"文件保存成功: {Path.GetFileName(selectedFileName)}", "保存成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LogManager.Log("系统", $"已保存系统状态到: {selectedFileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存文件时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"保存按钮点击事件失败: {ex.Message}");
            }
        }
    }
}