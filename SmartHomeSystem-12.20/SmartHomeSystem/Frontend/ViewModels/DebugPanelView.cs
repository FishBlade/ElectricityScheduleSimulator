using SmartHomeSystem.Backend.Framework;
using SmartHomeSystem.Frontend.ViewModels;
using static SmartHomeSystem.Backend.Framework.Topology;
using static SmartHomeSystem.Backend.Framework.Topology.BaseNode;

namespace SmartHomeSystem.Connection
{
    public partial class MainViewModel
    {
        // 私有字段定义
        private string _deviceId = "无";
        private double _devicePerformance;
        private double _devicePowerConsumption;
        private double _devicePowerProduction;
        private bool _deviceSwitchStatus;
        private string _deviceType = "无";
        private double _nodeMaxPower;
        private string _nodePowerPlan = "普通";
        private bool _nodeSwitchStatus;
        private string _parentNodeId = "无";

        #region XAML 绑定属性

        public double NodeMaxPower { get => _nodeMaxPower; set { _nodeMaxPower = value; OnPropertyChanged(nameof(NodeMaxPower)); } }
        public string NodePowerPlan { get => _nodePowerPlan; set { _nodePowerPlan = value; OnPropertyChanged(nameof(NodePowerPlan)); } }
        public bool NodeSwitchStatus { get => _nodeSwitchStatus; set { _nodeSwitchStatus = value; OnPropertyChanged(nameof(NodeSwitchStatus)); } }

        public string DeviceId { get => _deviceId; set { _deviceId = value; OnPropertyChanged(nameof(DeviceId)); } }
        public double DevicePerformance { get => _devicePerformance; set { _devicePerformance = value; OnPropertyChanged(nameof(DevicePerformance)); } }
        public double DevicePowerConsumption { get => _devicePowerConsumption; set { _devicePowerConsumption = value; OnPropertyChanged(nameof(DevicePowerConsumption)); } }
        public double DevicePowerProduction { get => _devicePowerProduction; set { _devicePowerProduction = value; OnPropertyChanged(nameof(DevicePowerProduction)); } }
        public bool DeviceSwitchStatus { get => _deviceSwitchStatus; set { _deviceSwitchStatus = value; OnPropertyChanged(nameof(DeviceSwitchStatus)); } }
        public string DeviceType { get => _deviceType; set { _deviceType = value; OnPropertyChanged(nameof(DeviceType)); } }
        public string ParentNodeId { get => _parentNodeId; set { _parentNodeId = value; OnPropertyChanged(nameof(ParentNodeId)); } }

        #endregion

        #region 逻辑处理

        public void UpdateEditorValues()
        {
            var node = Frontend.FrontContext.selectNode;
            if (node == null)
            {
                ClearAllValues();
                return;
            }

            // 1. 节点通用属性
            NodeSwitchStatus = node.nodeSwitch;
            NodeMaxPower = node is LineNode ln ? Math.Round(ln.maxPower, 2) : 0;

            // 2. 设备特定属性 使用模式匹配
            if (node is DeviceNode dn && dn.currentDevice is { } device)
            {
                DeviceSwitchStatus = device.switchon;
                DevicePerformance = Math.Round(device.performanceRating * 100, 2);
                DeviceType = device.GetType().Name;
                DeviceId = device.id.ToString();
                ParentNodeId = dn.parentNode?.GetHashCode().ToString() ?? "无";

                NodePowerPlan = dn.powerplan switch
                {
                    PowerPlan.Critical => "关键",
                    PowerPlan.Important => "重要",
                    PowerPlan.Low => "低优先级",
                    _ => "普通"
                };

                // 根据设备类型更新功率
                DevicePowerConsumption = device is BaseConsumer c ? Math.Round(c.realpowerConsumption, 2) : 0;
                DevicePowerProduction = device is BaseProducer p ? Math.Round(p.realpowerProduction, 2) : 0;
            }
            else
            {
                ClearDeviceValues();
                NodePowerPlan = "普通";
            }

            NotifyVisualStates();
        }

        private void ApplyNodeChanges()
        {
            var node = Frontend.FrontContext.selectNode;
            if (node == null) return;

            // 应用开关
            if (NodeSwitchStatus) node.Open(); else node.Close();

            // 应用最大功率 (仅线路节点)
            if (node is LineNode ln) ln.maxPower = NodeMaxPower;

            // 应用优先级 (仅设备节点)
            if (node is DeviceNode dn)
            {
                dn.powerplan = NodePowerPlan switch
                {
                    "关键" => PowerPlan.Critical,
                    "重要" => PowerPlan.Important,
                    "低优先级" => PowerPlan.Low,
                    _ => PowerPlan.Normal
                };
            }

            FinalizeUpdates();
        }

        private void ApplyDeviceChanges()
        {
            if (Frontend.FrontContext.selectNode is not DeviceNode dn || dn.currentDevice == null) return;
            var device = dn.currentDevice;

            // 应用开关
            device.switchon = DeviceSwitchStatus;
            dn.nodeSwitch = DeviceSwitchStatus;

            // 应用性能 (限制在 0-1 之间)
            device.performanceRating = Math.Clamp(DevicePerformance / 100.0, 0, 1);

            // 应用功率
            if (device is BaseConsumer c)
            {
                c.powerConsumption = DevicePowerConsumption;
                c.ConsumerformanceUpdate();
            }
            else if (device is BaseProducer p)
            {
                p.powerProduction = DevicePowerProduction;
                p.ProducerformanceUpdate();
            }

            FinalizeUpdates();
        }

        // 统一的后续处理逻辑，减少重复代码
        private void FinalizeUpdates()
        {
            Topology.CalculateNodePower();
            RefreshTreeDataOnly();   // 刷新树数值
            UpdateEditorValues();    // 回显修正后的数值
            LowCostUpdate();         // 触发全局UI刷新
        }

        private void ClearAllValues()
        {
            NodeSwitchStatus = false;
            NodeMaxPower = 0;
            NodePowerPlan = "普通";
            ClearDeviceValues();
            NotifyVisualStates();
        }

        private void ClearDeviceValues()
        {
            DeviceSwitchStatus = false;
            DevicePerformance = 0;
            DevicePowerConsumption = 0;
            DevicePowerProduction = 0;
            DeviceType = "无";
            DeviceId = "无";
            ParentNodeId = "无";
        }

        private void NotifyVisualStates()
        {
            OnPropertyChanged(nameof(SelectedNodeInfo));
            OnPropertyChanged(nameof(SelectedDeviceInfo));
            OnPropertyChanged(nameof(IsConsumerVisible));
            OnPropertyChanged(nameof(IsProducerVisible));
        }

        #endregion
    }
}