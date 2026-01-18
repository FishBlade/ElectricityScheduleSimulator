// ScheduleAlgorithm.cs
using SmartHomeSystem.Backend.Framework;

namespace SmartHomeSystem.Backend.Function
{
    public static class Schedule
    {
        #region 常量定义

        public const int RECOVERY_LIMIT = 4;
        public static int RecoveryCount = 0;
        public static double RecoveryRatio = 1;
        private const double DEFAULT_PRICE = 0.55;

        #endregion 常量定义

        #region 电价感知配置

        private static double _averagePriceThreshold = DEFAULT_PRICE;
        private static double _minPrice = DEFAULT_PRICE;

        #endregion 电价感知配置

        #region 调度容器

        private static readonly Stack<Topology.LineNode> _overloadStack = new Stack<Topology.LineNode>();
        private static readonly HashSet<Topology.LineNode> _processedNodes = new HashSet<Topology.LineNode>();
        private static readonly Queue<Topology.DeviceNode> _recoveryQueue = new Queue<Topology.DeviceNode>();

        private static readonly List<Dictionary<int, Stack<Topology.DeviceNode>>> _suspendQueueGroups =
            new List<Dictionary<int, Stack<Topology.DeviceNode>>>();

        private static int _currentRecoveryStartIndex = 0;
        private static int _recoveryCycleCounter = 0;
        private static int _scheduleExecutionCount = 0;

        #endregion 调度容器

        #region 内部类定义

        private class ScheduleNode
        {
            public List<ScheduleNode> Children { get; } = new List<ScheduleNode>();
            public double CurrentPower => OriginalNode.PowerConsumption;
            public List<Topology.DeviceNode> Devices { get; } = new List<Topology.DeviceNode>();
            public bool IsOverloaded => Target > 0;
            public double MaxPower => OriginalNode.maxPower;
            public Topology.LineNode OriginalNode { get; set; }
            public ScheduleNode Parent { get; set; }
            public double Target { get; set; } // 剩余目标值 = max(0, 当前功率 - 最大功率)
        }

        #endregion 内部类定义

        static Schedule()
        {
            InitializeSuspendQueueGroups();
            InitializePriceAwareness();
        }

        #region 初始化方法

        private static void InitializePriceAwareness()
        {
            Topology.PriceHistory.Clear();
            _minPrice = DEFAULT_PRICE;
            _averagePriceThreshold = DEFAULT_PRICE;
            LogManager.Log("电价感知", $"电价感知系统初始化完成，默认电价: {DEFAULT_PRICE}，最低电价: {_minPrice}");
        }

        private static void InitializeSuspendQueueGroups()
        {
            for (int cycle = 0; cycle < RECOVERY_LIMIT; cycle++)
            {
                var cycleGroup = new Dictionary<int, Stack<Topology.DeviceNode>>();

                for (int priority = 0; priority <= 3; priority++)
                    cycleGroup[priority] = new Stack<Topology.DeviceNode>();

                _suspendQueueGroups.Add(cycleGroup);
            }

            LogManager.Log("调度初始化",
                $"防饥饿挂起队列组初始化完成，共{RECOVERY_LIMIT}个周期组，每个周期组包含优先级范围: 0-3");
        }

        #endregion 初始化方法

        #region 主调度入口

        public static void ExecuteScheduling()
        {
            try
            {
                _scheduleExecutionCount++; // 递增调度计数器
                LogManager.Log("调度开始",
                    $"开始执行第{_scheduleExecutionCount}次调度算法 " +
                    $"(恢复周期: {RECOVERY_LIMIT}, 当前周期计数: {_recoveryCycleCounter})");

                UpdatePriceInformation();
                ClearSchedulingContainers();
                BuildOverloadStack();

                LogManager.Log("调度状态", $"检测到 {_overloadStack.Count} 个过载节点");

                ProcessOverloadNodes();
                ExecuteRecoveryStrategy();

                // 修复：每次调度完成后递增恢复周期计数器
                _recoveryCycleCounter = (_recoveryCycleCounter + 1) % RECOVERY_LIMIT;
                LogManager.Log("周期更新", $"恢复周期计数器更新为: {_recoveryCycleCounter}");

                LogManager.Log("调度完成", $"第{_scheduleExecutionCount}次调度算法执行完毕");
            }
            catch (Exception ex)
            {
                LogManager.Log("调度错误", $"调度算法执行失败: {ex.Message}");
            }
        }

        #endregion 主调度入口

        #region 步骤1: 电价信息更新

        private static void UpdatePriceInformation()
        {
            double currentPrice = ElectricityPricing.GetCurrentPrice();

            if (currentPrice < _minPrice)
            {
                _minPrice = currentPrice;
                LogManager.Log("电价更新", $"发现新的最低电价: {_minPrice}");
            }

            _averagePriceThreshold = (DEFAULT_PRICE + _minPrice) / 2.0;

            LogManager.Log("电价状态",
                $"当前电价: {currentPrice}, 最低电价: {_minPrice}, " +
                $"平均阈值: {_averagePriceThreshold}, 历史记录数: {Topology.PriceHistory.Count}");
        }

        #endregion 步骤1: 电价信息更新

        #region 步骤2: 清空调度容器

        private static void CleanupSuspendQueues()
        {
            for (int cycle = 0; cycle < _suspendQueueGroups.Count; cycle++)
            {
                foreach (var priority in _suspendQueueGroups[cycle].Keys.ToList())
                {
                    var validDevices = new Stack<Topology.DeviceNode>();

                    while (_suspendQueueGroups[cycle][priority].Count > 0)
                    {
                        var device = _suspendQueueGroups[cycle][priority].Pop();

                        if (IsValidSuspendedDevice(device))
                        {
                            validDevices.Push(device);
                        }
                    }

                    _suspendQueueGroups[cycle][priority] = validDevices;
                }
            }
        }

        private static void ClearSchedulingContainers()
        {
            _overloadStack.Clear();
            _processedNodes.Clear();
            _recoveryQueue.Clear();
            CleanupSuspendQueues();
        }

        private static bool IsValidSuspendedDevice(Topology.DeviceNode device)
        {
            return device?.currentDevice != null &&
                   (device.currentDevice is Consumer) &&
                   !device.nodeSwitch;
        }

        #endregion 步骤2: 清空调度容器

        #region 步骤3: 构建过载节点栈

        private static void BuildOverloadStack()
        {
            PostorderTraverse(Topology.root);
        }

        private static void PostorderTraverse(Topology.LineNode node)
        {
            if (node == null) return;

            foreach (var child in node.childrenNode)
            {
                PostorderTraverse(child);
            }

            bool isOverloaded = node.PowerConsumption > node.maxPower;
            node.isOverLoad = isOverloaded;

            if (isOverloaded)
            {
                _overloadStack.Push(node);
                LogManager.Log("过载检测", $"节点 {node.name} 过载: {node.PowerConsumption}/{node.maxPower} KW");
            }
        }

        #endregion 步骤3: 构建过载节点栈

        #region 步骤4: 处理过载节点

        private static void ProcessOverloadNodes()
        {
            int processedCount = 0;
            int totalNodes = _overloadStack.Count;

            while (_overloadStack.Count > 0)
            {
                var currentNode = _overloadStack.Pop();
                processedCount++;

                LogManager.Log("调度处理", $"处理节点 {currentNode.name} ({processedCount}/{totalNodes})");

                if (!ShouldProcessNode(currentNode))
                    continue;

                var scheduleTree = BuildAndSortScheduleTree(currentNode);
                ExecuteRemovalStrategy(scheduleTree, currentNode);
                _processedNodes.Add(currentNode);

                LogManager.Log("调度完成", $"节点 {currentNode.name} 处理完成");
            }
        }

        private static bool ShouldProcessNode(Topology.LineNode node)
        {
            if (_processedNodes.Contains(node))
            {
                LogManager.Log("调度跳过", $"节点 {node.name} 已处理，跳过");
                return false;
            }

            if (!node.isOverLoad || node.PowerConsumption <= node.maxPower)
            {
                _processedNodes.Add(node);
                LogManager.Log("调度跳过", $"节点 {node.name} 已恢复正常，跳过");
                return false;
            }

            return true;
        }

        #endregion 步骤4: 处理过载节点

        #region 步骤5: 构建并排序调度树

        private static ScheduleNode BuildAndSortScheduleTree(Topology.LineNode topologyNode)
        {
            var scheduleRoot = BuildScheduleTreeRecursive(topologyNode, null);
            SortScheduleTree(scheduleRoot);
            return scheduleRoot;
        }

        private static ScheduleNode BuildScheduleTreeRecursive(Topology.LineNode topologyNode, ScheduleNode parent)
        {
            var scheduleNode = new ScheduleNode
            {
                OriginalNode = topologyNode,
                Parent = parent,
                Target = Math.Max(0, topologyNode.PowerConsumption - topologyNode.maxPower)
            };

            CollectAllDevicesRecursive(topologyNode, scheduleNode.Devices);

            foreach (var child in topologyNode.childrenNode)
            {
                var childScheduleNode = BuildScheduleTreeRecursive(child, scheduleNode);
                scheduleNode.Children.Add(childScheduleNode);
            }

            LogManager.Log("调度树构建",
                $"节点 {topologyNode.name} 包含 {scheduleNode.Devices.Count} 个设备, " +
                $"{scheduleNode.Children.Count} 个子节点, 目标值: {scheduleNode.Target}");

            return scheduleNode;
        }

        private static void CollectAllDevicesRecursive(Topology.LineNode node, List<Topology.DeviceNode> deviceList)
        {
            foreach (var device in node.devices)
            {
                if ((device.currentDevice is Consumer) && device.nodeSwitch)
                {
                    deviceList.Add(device);
                }
            }

            foreach (var child in node.childrenNode)
            {
                CollectAllDevicesRecursive(child, deviceList);
            }
        }

        private static void SortChildNodesByOverloadStatus(List<ScheduleNode> children)
        {
            children.Sort((a, b) =>
            {
                if (a.IsOverloaded && b.IsOverloaded)
                    return b.Target.CompareTo(a.Target);

                if (a.IsOverloaded) return -1;
                if (b.IsOverloaded) return 1;

                return a.CurrentPower.CompareTo(b.CurrentPower);
            });
        }

        private static void SortDevicesByPriority(List<Topology.DeviceNode> devices)
        {
            devices.Sort((a, b) =>
            {
                int priorityCompare = GetPriorityValue(a.powerplan).CompareTo(GetPriorityValue(b.powerplan));
                if (priorityCompare != 0) return priorityCompare;

                double powerA = GetDevicePower(a);
                double powerB = GetDevicePower(b);
                return powerA.CompareTo(powerB);
            });
        }

        private static void SortScheduleTree(ScheduleNode node)
        {
            if (node == null) return;

            SortDevicesByPriority(node.Devices);
            SortChildNodesByOverloadStatus(node.Children);

            foreach (var child in node.Children)
            {
                SortScheduleTree(child);
            }
        }

        #endregion 步骤5: 构建并排序调度树

        #region 步骤6: 移除策略

        private static void CheckAndMarkResolvedNodes(Topology.LineNode originalNode)
        {
            foreach (var child in originalNode.childrenNode)
            {
                if (!child.isOverLoad && _processedNodes.Contains(child))
                {
                    MoveNodeToEnd(originalNode, child);
                    LogManager.Log("节点恢复", $"子节点 {child.name} 已恢复正常，移至末尾");
                }
            }
        }

        private static void ExecuteDeviceRemoval(Topology.DeviceNode device, double devicePower, Topology.LineNode affectedNode)
        {
            device.currentDevice!.switchon = false;
            device.nodeSwitch = false;

            if (device.powerplan == Topology.BaseNode.PowerPlan.Critical)
            {
                LogManager.Log("调度警告", $"关闭关键设备: {device.currentDevice.name}");
            }

            AddDeviceToSuspendQueue(device);
            Topology.CalculateNodePower();
            affectedNode.isOverLoad = affectedNode.PowerConsumption > affectedNode.maxPower;
        }

        private static void ExecuteRemovalStrategy(ScheduleNode scheduleRoot, Topology.LineNode originalNode)
        {
            int removalCount = 0;
            double totalRemovedPower = 0;
            const int MAX_REMOVAL_ATTEMPTS = 100;

            while (scheduleRoot.IsOverloaded && scheduleRoot.Target > 0 && removalCount < MAX_REMOVAL_ATTEMPTS)
            {
                bool removed = TryRemoveLeafDevice(scheduleRoot, originalNode, out double removedPower);
                if (removed)
                {
                    removalCount++;
                    totalRemovedPower += removedPower;
                    LogManager.Log("移除进度",
                        $"已移除 {removalCount} 个设备，减少功率 {totalRemovedPower} KW，目标剩余: {scheduleRoot.Target} KW");
                }
                else
                {
                    LogManager.Log("调度警告", "无法找到可移除的设备，调度终止");
                    break;
                }

                CheckAndMarkResolvedNodes(originalNode);
            }

            LogManager.Log("移除完成", $"共移除 {removalCount} 个设备，总减少功率 {totalRemovedPower} KW");
        }

        private static Topology.DeviceNode FindBestLeafDeviceToRemove(ScheduleNode node)
        {
            foreach (var child in node.Children.Where(c => c.IsOverloaded))
            {
                var device = FindBestLeafDeviceToRemove(child);
                if (device != null) return device;
            }

            foreach (var device in node.Devices)
            {
                if (device.currentDevice?.switchon == true && (device.currentDevice is Consumer))
                {
                    return device;
                }
            }

            foreach (var child in node.Children.Where(c => !c.IsOverloaded))
            {
                var device = FindBestLeafDeviceToRemove(child);
                if (device != null) return device;
            }

            return null;
        }

        private static void MoveNodeToEnd(Topology.LineNode parent, Topology.LineNode child)
        {
            if (parent.childrenNode.Remove(child))
            {
                parent.childrenNode.Add(child);
            }
        }

        private static double RemoveDevice(Topology.DeviceNode device, Topology.LineNode originalNode)
        {
            if (device.currentDevice is not Consumer consumer)
                return 0;

            double devicePower = consumer.realpowerConsumption;
            ExecuteDeviceRemoval(device, devicePower, originalNode);
            return devicePower;
        }

        private static bool TryRemoveLeafDevice(ScheduleNode currentNode, Topology.LineNode originalNode, out double removedPower)
        {
            removedPower = 0;
            var leafDevice = FindBestLeafDeviceToRemove(currentNode);

            if (leafDevice == null)
                return false;

            removedPower = RemoveDevice(leafDevice, originalNode);
            UpdateScheduleTreeTarget(currentNode, -removedPower);

            return true;
        }

        private static void UpdateScheduleTreeTarget(ScheduleNode node, double delta)
        {
            var current = node;
            while (current != null)
            {
                current.Target = Math.Max(0, current.Target + delta);
                current = current.Parent;
            }
        }

        #endregion 步骤6: 移除策略

        #region 步骤7: 防饥饿恢复策略

        private static bool CheckOverloadAfterRecovery()
        {
            Topology.CalculateNodePower();

            bool rootOverload = Topology.root.PowerConsumption > Topology.root.maxPower;
            bool childOverload = Topology.root.childrenNode.Any(child => child.PowerConsumption > child.maxPower);

            if (rootOverload || childOverload)
            {
                LogManager.Log("过载检测", "设备恢复导致系统过载");
                return true;
            }

            return false;
        }

        private static void ExecuteRecoveryStrategy()
        {
            if (_overloadStack.Count > 0)
            {
                LogManager.Log("恢复跳过", "仍有过载节点，跳过恢复阶段");
                return;
            }

            LogManager.Log("恢复开始", "开始执行防饥饿轮询恢复策略");

            double currentRecoveryRatio = CalculatePriceRecoveryRatio();
            RecoveryResult result = PerformDeviceRecovery(currentRecoveryRatio);

            LogManager.Log("恢复完成",
                $"共恢复 {result.RecoveredCount} 个设备，" +
                $"总恢复功率 {result.TotalRecoveredPower} KW，" +
                $"恢复比例: {currentRecoveryRatio:P0}");
        }

        private static RecoveryResult PerformDeviceRecovery(double recoveryRatio)
        {
            var result = new RecoveryResult();

            for (int offset = 0; offset < RECOVERY_LIMIT && !result.OverloadOccurred; offset++)
            {
                int currentCycleIndex = (_currentRecoveryStartIndex + offset) % RECOVERY_LIMIT;
                LogManager.Log("恢复轮询",
                    $"处理周期组 {currentCycleIndex} " +
                    $"(起始索引: {_currentRecoveryStartIndex}, 偏移: {offset}, 当前周期: {_recoveryCycleCounter})");

                result.OverloadOccurred = ProcessCycleGroupRecovery(currentCycleIndex, recoveryRatio, result);

                if (result.OverloadOccurred)
                {
                    LogManager.Log("恢复停止", "设备恢复导致系统过载，停止恢复");
                    break;
                }
            }

            return result;
        }

        private static bool ProcessCycleGroupRecovery(int cycleIndex, double recoveryRatio, RecoveryResult result)
        {
            for (int priority = 0; priority <= 2; priority++)
            {
                if (!TryGetSuspendQueue(cycleIndex, priority, out var queue) || queue.Count == 0)
                    continue;

                var device = queue.Pop();
                if (TryRecoverSingleDevice(device, recoveryRatio, out double recoveredPower))
                {
                    result.RecoveredCount++;
                    result.TotalRecoveredPower += recoveredPower;

                    LogManager.Log("设备恢复",
                        $"从周期组{cycleIndex}恢复设备 {device.currentDevice.name} (优先级{priority})，" +
                        $"功率: {recoveredPower} KW");

                    if (CheckOverloadAfterRecovery())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetSuspendQueue(int cycleIndex, int priority, out Stack<Topology.DeviceNode> queue)
        {
            queue = null;

            if (cycleIndex >= _suspendQueueGroups.Count ||
                !_suspendQueueGroups[cycleIndex].ContainsKey(priority))
                return false;

            queue = _suspendQueueGroups[cycleIndex][priority];
            return true;
        }

        private static bool TryRecoverSingleDevice(Topology.DeviceNode device, double recoveryRatio, out double recoveredPower)
        {
            recoveredPower = 0;

            if (device?.currentDevice == null || device.currentDevice is not Consumer consumer)
                return false;

            double maxPower = consumer.realpowerConsumption;
            recoveredPower = maxPower * recoveryRatio;

            device.currentDevice.switchon = true;
            device.nodeSwitch = true;

            LogManager.Log("设备恢复详情",
                $"恢复设备 {device.currentDevice.name}，" +
                $"最大功率: {maxPower}KW，恢复比例: {recoveryRatio:P0}，" +
                $"实际恢复: {recoveredPower}KW");

            return true;
        }

        private class RecoveryResult
        {
            public bool OverloadOccurred { get; set; }
            public int RecoveredCount { get; set; }
            public double TotalRecoveredPower { get; set; }
        }

        #endregion 步骤7: 防饥饿恢复策略

        #region 电价感知方法

        private static double CalculatePriceRecoveryRatio()
        {
            double currentPrice = ElectricityPricing.GetCurrentPrice();

            if (currentPrice <= _averagePriceThreshold)
            {
                LogManager.Log("电价恢复", $"电价{currentPrice}低于阈值{_averagePriceThreshold}，正常恢复(100%)");
                return 1.0;
            }

            double priceRatio = currentPrice / _averagePriceThreshold;
            double recoveryRatio = 1.0 / priceRatio;
            recoveryRatio = Math.Max(recoveryRatio, 0.3);

            LogManager.Log("电价恢复",
                $"电价{currentPrice}高于阈值{_averagePriceThreshold}，" +
                $"比例: {priceRatio:P0}，恢复比例: {recoveryRatio:P0}");

            return recoveryRatio;
        }

        #endregion 电价感知方法

        #region 挂起队列管理

        private static void AddDeviceToSuspendQueue(Topology.DeviceNode device)
        {
            if (device.powerplan == Topology.BaseNode.PowerPlan.Low)
                return;

            int priorityGroup = GetPriorityValue(device.powerplan);
            // 修复：使用当前恢复周期计数器来确定设备应该进入哪个周期组
            int cycleGroupIndex = _recoveryCycleCounter % RECOVERY_LIMIT;

            if (cycleGroupIndex < _suspendQueueGroups.Count &&
                _suspendQueueGroups[cycleGroupIndex].ContainsKey(priorityGroup))
            {
                _suspendQueueGroups[cycleGroupIndex][priorityGroup].Push(device);
                LogManager.Log("挂起队列",
                    $"设备 {device.currentDevice.name} 加入周期组{cycleGroupIndex}的优先级{priorityGroup}队列 " +
                    $"(将在{cycleGroupIndex}个周期后有机会恢复)");
            }
        }

        #endregion 挂起队列管理

        #region 公共接口方法

        public static int GetCurrentRecoveryCycle()
        {
            return _recoveryCycleCounter;
        }

        public static IEnumerable<Topology.LineNode> GetOverloadedNodes()
        {
            return _overloadStack.ToArray();
        }

        public static int GetScheduleExecutionCount()
        {
            return _scheduleExecutionCount;
        }

        public static Dictionary<int, IEnumerable<Topology.DeviceNode>> GetSuspendedDevices()
        {
            var result = new Dictionary<int, IEnumerable<Topology.DeviceNode>>();

            for (int priority = 0; priority <= 3; priority++)
            {
                var allDevices = new List<Topology.DeviceNode>();

                for (int cycle = 0; cycle < _suspendQueueGroups.Count; cycle++)
                {
                    if (_suspendQueueGroups[cycle].ContainsKey(priority))
                    {
                        allDevices.AddRange(_suspendQueueGroups[cycle][priority]);
                    }
                }

                result[priority] = allDevices;
            }

            return result;
        }

        #endregion 公共接口方法

        #region 辅助方法

        private static double GetDevicePower(Topology.DeviceNode device)
        {
            return device.currentDevice switch
            {
                Consumer consumer => consumer.realpowerConsumption,
                Producer producer => producer.realpowerProduction,
                _ => 0
            };
        }

        private static int GetPriorityValue(Topology.BaseNode.PowerPlan plan)
        {
            return plan switch
            {
                Topology.BaseNode.PowerPlan.Critical => 0,
                Topology.BaseNode.PowerPlan.Important => 1,
                Topology.BaseNode.PowerPlan.Normal => 2,
                Topology.BaseNode.PowerPlan.Low => 3,
                _ => 4
            };
        }

        #endregion 辅助方法
    }
}