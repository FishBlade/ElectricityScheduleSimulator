using SmartHomeSystem.Backend.Framework;
using System.IO;

namespace SmartHomeSystem.Backend.Function
{
    public static class SaveManager
    {
        public static Dictionary<int, (int, Topology.BaseNode)> NodeDic = new Dictionary<int, (int, Topology.BaseNode)>();
        public static Dictionary<int, (int, BaseDevice)> DeviceDic = new Dictionary<int, (int, BaseDevice)>();
        public static int NodeID;
        public static int DeviceID;
        public static DateTime time;

        #region 保存功能

        public static void Save()
        {
            string filename = $"{DateTime.Now:yyyyMMdd_HHmmss}.sav";
            Save(filename);
        }

        public static void Save(string filename)
        {
            NodeID = 0;
            DeviceID = 0;
            NodeDic.Clear();
            DeviceDic.Clear();

            // 序列化拓扑结构
            SaveHelper(-1, Topology.root);
            time = DateTime.Now;

            try
            {
                using (StreamWriter writer = new StreamWriter(filename))
                {
                    writer.WriteLine("<HEAD>");

                    // 统一格式：标签单独一行，数据在下一行
                    writer.WriteLine("<timeScale>");
                    writer.WriteLine(GetTimeScaleLabel());

                    writer.WriteLine("<virtualTime>");
                    writer.WriteLine($"{Time.virtualTime:yyyy-MM-dd HH:mm:ss}");

                    writer.WriteLine("<startTime>");
                    writer.WriteLine($"{Time.startTime:yyyy-MM-dd HH:mm:ss}");

                    writer.WriteLine("<ElectricityConsumption>");
                    writer.WriteLine($"{Topology.cumulativeElectricityConsumption:F6}");

                    writer.WriteLine("<ElectricityPricing>");
                    writer.WriteLine($"{Topology.cumulativeElectricityPricing:F6}");

                    // 统计数据
                    SaveStatistics(writer);

                    // 树结构
                    SaveTreeStructure(writer);

                    // 设备数据
                    SaveDeviceData(writer);

                    // 调度器状态
                    SaveSchedulerState(writer);

                    writer.WriteLine("<TAIL>");
                }

                LogManager.Log("保存成功", $"系统状态已保存到文件: {filename}");
            }
            catch (Exception ex)
            {
                LogManager.Log("保存错误", $"保存文件失败: {ex.Message}");
            }
        }

        private static void SaveHelper(int parentId, Topology.BaseNode parentNode)
        {
            if (parentNode != null)
            {
                if (parentNode is Topology.DeviceNode deviceNode)
                {
                    if (deviceNode.currentDevice != null)
                    {
                        DeviceDic.Add(DeviceID, (parentId, deviceNode.currentDevice));
                        DeviceID++;
                    }
                }
                else if (parentNode is Topology.LineNode lineNode)
                {
                    NodeDic.Add(NodeID, (parentId, lineNode));
                    int currentParentId = NodeID;
                    NodeID++;
                    foreach (var dnode in lineNode.devices)
                        SaveHelper(currentParentId, dnode);
                    foreach (var lnode in lineNode.childrenNode)
                        SaveHelper(currentParentId, lnode);
                }
            }
        }

        private static void SaveStatistics(StreamWriter writer)
        {
            // 消费历史
            writer.WriteLine("<ConsumptionHistory>");
            writer.WriteLine($"{Topology.ConsumptionHistory.Count}");
            foreach (var (hour, consumption) in Topology.ConsumptionHistory)
            {
                writer.WriteLine($"{hour},{consumption:F6}");
            }

            // 资金历史
            writer.WriteLine("<MoneyHistory>");
            writer.WriteLine($"{Topology.MoneyHistory.Count}");
            foreach (var (hour, money) in Topology.MoneyHistory)
            {
                writer.WriteLine($"{hour},{money:F6}");
            }

            // 价格历史
            writer.WriteLine("<PriceHistory>");
            writer.WriteLine($"{Topology.PriceHistory.Count}");
            foreach (var (hour, price) in Topology.PriceHistory)
            {
                writer.WriteLine($"{hour},{price:F6}");
            }
        }

        private static void SaveTreeStructure(StreamWriter writer)
        {
            writer.WriteLine("<TREE>");
            writer.WriteLine($"{NodeDic.Count}");
            foreach (var kvp in NodeDic)
            {
                var (parentId, node) = kvp.Value;
                if (node is Topology.LineNode lineNode)
                {
                    writer.WriteLine($"{kvp.Key},{parentId},{SerializeLineNode(lineNode)}");
                }
            }
        }

        private static void SaveDeviceData(StreamWriter writer)
        {
            writer.WriteLine("<DEVICE>");
            writer.WriteLine($"{DeviceDic.Count}");
            foreach (var kvp in DeviceDic)
            {
                var (parentId, device) = kvp.Value;
                writer.WriteLine($"{kvp.Key},{parentId},{SerializeDevice(device)}");
            }
        }

        private static void SaveSchedulerState(StreamWriter writer)
        {
            // 过载栈
            var overloadStack = GetOverloadStackIds();
            writer.WriteLine("<overloadStack>");
            writer.WriteLine(overloadStack.Any() ? string.Join(" ", overloadStack) : "");

            // 恢复队列
            var recoveryQueue = GetRecoveryQueueIds();
            writer.WriteLine("<recoverQueue>");
            writer.WriteLine(recoveryQueue.Any() ? string.Join(" ", recoveryQueue) : "");

            // 挂起队列组
            writer.WriteLine("<suspendQueueGroups>");
            var suspendGroups = GetSuspendQueueGroups();
            foreach (var (cycle, priorityDict) in suspendGroups)
            {
                writer.WriteLine($"<cycle>{cycle}");
                foreach (var (priority, deviceIds) in priorityDict)
                {
                    writer.WriteLine($"<powergroup>{priority}");
                    writer.WriteLine(deviceIds.Any() ? string.Join(" ", deviceIds) : "");
                }
            }
        }

        #endregion 保存功能

        #region 序列化辅助方法

        private static string SerializeLineNode(Topology.LineNode node)
        {
            return $"{EscapeString(node.name)}|{node.nodeSwitch}|{node.maxPower:F6}|{node.type}|{node.isOverLoad}|{node.isPowered}";
        }

        private static string SerializeDevice(BaseDevice device)
        {
            string deviceType = device.GetType().Name;
            string additionalData = "";

            string baseInfo = $"{device.id}|{EscapeString(device.name)}|{device.switchon}|{device.performanceRating:F6}|{device.type}|{deviceType}";

            if (device is BaseConsumer consumer)
                additionalData = $"|{consumer.powerConsumption:F6}|{consumer.realpowerConsumption:F6}";
            else if (device is BaseProducer producer)
                additionalData = $"|{producer.powerProduction:F6}|{producer.realpowerProduction:F6}";
            else if (device is BaseBattery battery)
                additionalData = $"|{battery.capacity:F6}|{battery.currentCharge:F6}|{battery.usingbattery}|{battery.realcapacity:F6}";

            return baseInfo + additionalData;
        }

        private static string EscapeString(string str)
        {
            return str?.Replace("|", "||").Replace(",", "|,") ?? "";
        }

        #endregion 序列化辅助方法

        #region 工具方法

        // 获取所有保存文件列表
        public static List<string> GetSaveFiles()
        {
            return Directory.GetFiles(".", "*.sav")
                           .Select(Path.GetFileName)
                           .OrderDescending()
                           .ToList();
        }

        // 快速保存（使用默认文件名）
        public static void QuickSave()
        {
            Save();
        }

        private static string GetTimeScaleLabel()
        {
            // 根据Time类的实际实现获取当前时间倍率
            return "实时";
        }

        private static List<int> GetOverloadStackIds()
        {
            var result = new List<int>();
            return result;
        }

        private static List<int> GetRecoveryQueueIds()
        {
            var result = new List<int>();
            return result;
        }

        private static Dictionary<int, Dictionary<int, List<int>>> GetSuspendQueueGroups()
        {
            var result = new Dictionary<int, Dictionary<int, List<int>>>();
            return result;
        }

        #endregion 工具方法
    }
}