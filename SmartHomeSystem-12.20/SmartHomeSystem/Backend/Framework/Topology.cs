using SmartHomeSystem.Backend.Function;

namespace SmartHomeSystem.Backend.Framework
{
    public class Topology
    {
        public static LineNode root = new LineNode()
        {
            type = NodeType.Root,
            name = "电网根节点"
        };

        #region 统计数据

        public static List<(int, double)> ConsumptionHistory = new List<(int, double)>();
        public static double cumulativeElectricityConsumption = 0;
        public static double cumulativeElectricityPricing = 0;

        public static List<(int, double)> MoneyHistory = new List<(int, double)> { (0, 0) };
        public static List<(int, double)> PriceHistory = new List<(int, double)>();

        #endregion 统计数据

        #region 设备容器

        public static Dictionary<Guid, Framework.BaseDevice> allService = new Dictionary<Guid, Framework.BaseDevice>();

        #endregion 设备容器

        static Topology()
        {
            root.maxPower = 1000;
            root.Open();
            Event.HourlyEvent += TopologyUpdate;
        }

        #region 节点定义

        public class BaseNode
        {
            public string name;
            public bool nodeSwitch = true;
            public LineNode parentNode;
            public NodeType type;

            public enum PowerPlan
            { Critical, Important, Normal, Low }

            public virtual void Close()
            {
            }

            public virtual void Open()
            {
            }

            public virtual void Toggle()
            {
            }

            public virtual void Delete()
            {
            }
        }

        public class DeviceNode : BaseNode
        {
            public BaseDevice? currentDevice = null;
            public PowerPlan powerplan;

            public DeviceNode(Framework.BaseDevice s)
            {
                currentDevice = s;
                type = s.type;
                nodeSwitch = s.switchon;
                powerplan = PowerPlan.Normal;
                name = s.name ?? "未命名设备";
            }

            public override void Close()
            {
                currentDevice!.switchon = false;
                nodeSwitch = false;
                if (powerplan == PowerPlan.Critical)
                    LogManager.Log("警告", "关闭关键设备");
            }

            public override void Open()
            {
                currentDevice!.switchon = true;
                this.nodeSwitch = true;
            }

            public override void Toggle()
            {
                if (nodeSwitch)
                    Close();
                else
                    Open();
            }

            public override void Delete()
            {
                // 从父节点的设备列表中移除自己
                if (parentNode != null)
                {
                    parentNode.devices.Remove(this);
                    parentNode = null; // 断开与父节点的连接
                }

                // 清理设备资源
                if (currentDevice != null)
                {
                    // 从全局设备容器中移除
                    if (allService.ContainsKey(currentDevice.id))
                    {
                        allService.Remove(currentDevice.id);
                    }
                    currentDevice = null;
                }

                LogManager.Log("设备删除", $"设备 '{name}' 已从拓扑中删除");
            }
        }

        public class LineNode : BaseNode
        {
            public List<LineNode> childrenNode;
            public List<DeviceNode> devices;
            public bool isOverLoad = false;
            public bool isPowered = false;
            public double maxPower = 0;
            public double PowerConsumption = 0;
            public double PowerProduction = 0;

            public LineNode()
            {
                childrenNode = new List<LineNode>();
                devices = new List<DeviceNode>();
                type = NodeType.Line;
                name = "未命名线路";
            }

            public void Add(Framework.BaseDevice s)
            {
                DeviceNode DN = new DeviceNode(s);
                devices.Add(DN);
                DN.parentNode = this;
            }

            public void Add(BaseNode node)
            {
                node.parentNode = this;
                if (node is LineNode L)
                    this.childrenNode.Add(L);
                else if (node is DeviceNode D)
                    this.devices.Add(D);
            }

            public void Add()
            {
                LineNode ln = new LineNode();
                childrenNode.Add(ln);
                ln.parentNode = this;
            }

            public (double consumption, double production) CalculateNodePowerHelper()
            {
                double totalConsumption = 0;
                double totalProduction = 0;

                foreach (var d in devices)
                {
                    if (d.currentDevice?.switchon == true && d.nodeSwitch == true)
                    {
                        if (d.currentDevice is Framework.Consumer consumer)
                            totalConsumption += consumer.realpowerConsumption;
                        if (d.currentDevice is Framework.Producer producer)
                            totalProduction += producer.realpowerProduction;
                    }
                }

                foreach (var child in childrenNode)
                {
                    if (child.nodeSwitch)
                    {
                        var (childConsumption, childProduction) = child.CalculateNodePowerHelper();
                        totalConsumption += childConsumption;
                        totalProduction += childProduction;
                    }
                }

                PowerConsumption = totalConsumption;
                PowerProduction = totalProduction;
                isOverLoad = PowerConsumption > maxPower;

                return (totalConsumption, totalProduction);
            }

            public override void Close()
            {
                foreach (var child in childrenNode) child.Close();
                foreach (var device in devices) device.Close();
            }

            public override void Open()
            {
                foreach (var child in childrenNode) child.Open();
                foreach (var device in devices) device.Open();
            }

            public override void Delete()
            {
                // 先递归删除所有子节点和设备
                foreach (var child in childrenNode.ToList()) // 使用ToList()避免修改集合时迭代
                {
                    child.Delete();
                }

                foreach (var device in devices.ToList())
                {
                    device.Delete();
                }

                // 从父节点中移除自己
                if (parentNode != null)
                {
                    parentNode.childrenNode.Remove(this);
                    parentNode = null; // 断开与父节点的连接
                }

                // 清理列表引用
                childrenNode.Clear();
                devices.Clear();

                LogManager.Log("线路删除", $"线路节点 '{name}' 已从拓扑中删除");
            }

            // 添加一个安全的删除子节点方法
            public void RemoveChild(LineNode child)
            {
                if (childrenNode.Contains(child))
                {
                    childrenNode.Remove(child);
                    child.parentNode = null;
                }
            }

            // 添加一个安全的删除设备方法
            public void RemoveDevice(DeviceNode device)
            {
                if (devices.Contains(device))
                {
                    devices.Remove(device);
                    device.parentNode = null;
                }
            }

            public override void Toggle()
            {
                if (nodeSwitch) Close();
                else Open();
            }
        }

        #endregion 节点定义

        #region 更新

        private static void TopologyUpdate()
        {
            CalculateNodePower();
            Schedule.ExecuteScheduling();
            CalculateNodePower();
            RootStatistics();
        }

        #endregion 更新

        #region 辅助方法

        public static void CalculateNodePower()
        {
            root.CalculateNodePowerHelper();
        }

        private static void RootStatistics()
        {
            int passHour = Time.GetPassHour();
            double consumption = Math.Max((root.PowerConsumption - root.PowerProduction), 0);
            double price = ElectricityPricing.GetCurrentPrice();
            double money = price * consumption;
            ConsumptionHistory.Add((passHour, consumption));
            MoneyHistory.Add((passHour, money));
            cumulativeElectricityConsumption += consumption;
            cumulativeElectricityPricing += money;
        }

        #endregion 辅助方法
    }
}