using Microsoft.Win32;
using SmartHomeSystem.Backend.Framework;
using System.IO;

namespace SmartHomeSystem.Backend.Function
{
    public static class LoadManager
    {
        #region 加载功能

        public static bool Load()
        {
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
                return Load(openFileDialog.FileName);
            }
            return false;
        }

        public static bool Load(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    LogManager.Log("加载错误", $"文件不存在: {filename}");
                    return false;
                }

                // 清空现有数据
                ClearExistingData();

                var nodeData = new Dictionary<int, (int parentId, string data)>();
                var deviceData = new Dictionary<int, (int parentId, string data)>();

                using (StreamReader reader = new StreamReader(filename))
                {
                    string line;
                    string currentSection = "";
                    int expectedCount = 0;
                    int currentCount = 0;
                    bool expectingDataLine = false;

                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line == "<HEAD>" || line == "<TAIL>") continue;

                        // 处理标签行
                        if (line.StartsWith("<") && line.EndsWith(">"))
                        {
                            currentSection = line;
                            expectingDataLine = true;
                            currentCount = 0;
                            continue;
                        }

                        if (string.IsNullOrEmpty(line)) continue;

                        // 处理数据行
                        if (expectingDataLine)
                        {
                            expectingDataLine = false;

                            if (IsDataSectionWithCount(currentSection))
                            {
                                // 多行数据段落：第一行是记录数量
                                if (int.TryParse(line, out int count))
                                {
                                    expectedCount = count;
                                    LogManager.Log("加载段落", $"进入段落: {currentSection}, 预期记录数: {expectedCount}");
                                }
                                else
                                {
                                    LogManager.Log("格式错误", $"无效的记录数量: {line}");
                                    expectedCount = 0;
                                }
                            }
                            else
                            {
                                // 单行配置：直接处理数据
                                ProcessSingleLineData(currentSection, line);
                                currentSection = "";
                            }
                        }
                        else
                        {
                            // 处理多行段落的数据行
                            ProcessSectionLine(line, currentSection, ref currentCount, expectedCount, nodeData, deviceData);
                        }
                    }
                }

                LogManager.Log("加载数据", $"读取到节点: {nodeData.Count}个, 设备: {deviceData.Count}个");

                // 重建拓扑结构
                bool success = RebuildTopology(nodeData, deviceData);
                if (success)
                {
                    Topology.CalculateNodePower();
                    LogManager.Log("加载成功",
                        $"拓扑结构重建成功，共{nodeData.Count}个节点，{deviceData.Count}个设备，" +
                        $"总功率: {Topology.root.PowerConsumption:F2}KW");

                    ValidateTopology();
                }
                else
                {
                    LogManager.Log("加载错误", "拓扑结构重建失败");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogManager.Log("加载错误", $"加载文件失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        // 判断是否是包含记录数量的数据段落
        private static bool IsDataSectionWithCount(string section)
        {
            return section == "<ConsumptionHistory>" ||
                   section == "<MoneyHistory>" ||
                   section == "<PriceHistory>" ||
                   section == "<TREE>" ||
                   section == "<DEVICE>";
        }

        // 处理单行配置数据
        private static void ProcessSingleLineData(string section, string data)
        {
            try
            {
                switch (section)
                {
                    case "<virtualTime>":
                        Time.virtualTime = DateTime.Parse(data);
                        LogManager.Log("配置加载", $"虚拟时间: {Time.virtualTime}");
                        break;

                    case "<startTime>":
                        Time.startTime = DateTime.Parse(data);
                        LogManager.Log("配置加载", $"开始时间: {Time.startTime}");
                        break;

                    case "<ElectricityConsumption>":
                        Topology.cumulativeElectricityConsumption = double.Parse(data);
                        LogManager.Log("配置加载", $"累计用电量: {Topology.cumulativeElectricityConsumption}");
                        break;

                    case "<ElectricityPricing>":
                        Topology.cumulativeElectricityPricing = double.Parse(data);
                        LogManager.Log("配置加载", $"累计电费: {Topology.cumulativeElectricityPricing}");
                        break;

                    default:
                        LogManager.Log("未知段落", $"跳过未知段落: {section}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Log("配置错误", $"处理配置失败: {section}={data}, 错误: {ex.Message}");
            }
        }

        private static void ClearExistingData()
        {
            Topology.ConsumptionHistory.Clear();
            Topology.MoneyHistory.Clear();
            Topology.PriceHistory.Clear();
            Topology.cumulativeElectricityConsumption = 0;
            Topology.cumulativeElectricityPricing = 0;

            Topology.root.childrenNode.Clear();
            Topology.root.devices.Clear();
            Topology.allService.Clear();
        }

        private static void ProcessSectionLine(string line, string currentSection, ref int currentCount, int expectedCount,
                                              Dictionary<int, (int, string)> nodeData,
                                              Dictionary<int, (int, string)> deviceData)
        {
            if (string.IsNullOrEmpty(line)) return;

            try
            {
                if (currentCount >= expectedCount)
                {
                    LogManager.Log("数据警告", $"超过预期记录数，跳过: {line}");
                    return;
                }

                switch (currentSection)
                {
                    case "<ConsumptionHistory>":
                        var consumptionParts = line.Split(',');
                        if (consumptionParts.Length == 2)
                            Topology.ConsumptionHistory.Add((int.Parse(consumptionParts[0]), double.Parse(consumptionParts[1])));
                        currentCount++;
                        break;

                    case "<MoneyHistory>":
                        var moneyParts = line.Split(',');
                        if (moneyParts.Length == 2)
                            Topology.MoneyHistory.Add((int.Parse(moneyParts[0]), double.Parse(moneyParts[1])));
                        currentCount++;
                        break;

                    case "<PriceHistory>":
                        var priceParts = line.Split(',');
                        if (priceParts.Length == 2)
                            Topology.PriceHistory.Add((int.Parse(priceParts[0]), double.Parse(priceParts[1])));
                        currentCount++;
                        break;

                    case "<TREE>":
                        var nodeParts = line.Split(',');
                        if (nodeParts.Length >= 3)
                        {
                            int nodeId = int.Parse(nodeParts[0]);
                            int parentId = int.Parse(nodeParts[1]);
                            string nodeDataStr = string.Join(",", nodeParts, 2, nodeParts.Length - 2);
                            nodeData[nodeId] = (parentId, nodeDataStr);
                            LogManager.Log("读取节点", $"ID: {nodeId}, 父ID: {parentId}");
                        }
                        currentCount++;
                        break;

                    case "<DEVICE>":
                        var deviceParts = line.Split(',');
                        if (deviceParts.Length >= 3)
                        {
                            int deviceId = int.Parse(deviceParts[0]);
                            int parentId = int.Parse(deviceParts[1]);
                            string deviceDataStr = string.Join(",", deviceParts, 2, deviceParts.Length - 2);
                            deviceData[deviceId] = (parentId, deviceDataStr);
                            LogManager.Log("读取设备", $"ID: {deviceId}, 父节点: {parentId}");
                        }
                        currentCount++;
                        break;

                    default:
                        LogManager.Log("未知段落", $"跳过未知段落的数据行: {currentSection} - {line}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogManager.Log("加载错误", $"处理行时出错: {line}, 错误: {ex.Message}");
            }
        }

        private static bool RebuildTopology(Dictionary<int, (int parentId, string data)> nodeData,
                                           Dictionary<int, (int parentId, string data)> deviceData)
        {
            try
            {
                var nodeMap = new Dictionary<int, Topology.LineNode>();
                nodeMap[-1] = Topology.root; // 根节点

                LogManager.Log("重建开始", $"开始重建拓扑结构，节点数: {nodeData.Count}");

                // 按层级顺序构建节点
                bool changed;
                int maxIterations = 100;
                int iteration = 0;

                do
                {
                    changed = false;
                    iteration++;

                    foreach (var kvp in nodeData.Where(x => !nodeMap.ContainsKey(x.Key)).ToList())
                    {
                        int nodeId = kvp.Key;
                        int parentId = kvp.Value.parentId;

                        if (nodeMap.ContainsKey(parentId))
                        {
                            var lineNode = CreateLineNodeFromString(kvp.Value.data);
                            if (lineNode != null)
                            {
                                nodeMap[parentId].childrenNode.Add(lineNode);
                                lineNode.parentNode = nodeMap[parentId];
                                nodeMap[nodeId] = lineNode;
                                changed = true;

                                LogManager.Log("建立节点关系", $"父节点 {parentId} -> 子节点 {nodeId}: {lineNode.name}");
                            }
                        }
                    }

                    if (iteration >= maxIterations)
                    {
                        LogManager.Log("重建警告", "达到最大迭代次数，可能存在循环依赖");
                        break;
                    }
                } while (changed);

                // 检查未处理的节点
                var unprocessedNodes = nodeData.Keys.Except(nodeMap.Keys).ToList();
                if (unprocessedNodes.Any())
                {
                    LogManager.Log("重建警告", $"发现孤立节点，无法连接到根节点: {string.Join(",", unprocessedNodes)}");
                }

                // 创建设备并添加到对应节点
                int deviceCount = 0;
                foreach (var kvp in deviceData)
                {
                    int deviceId = kvp.Key;
                    int parentNodeId = kvp.Value.parentId;
                    string deviceStr = kvp.Value.data;

                    if (nodeMap.TryGetValue(parentNodeId, out var parentNode))
                    {
                        var device = CreateDeviceFromString(deviceStr);
                        if (device != null)
                        {
                            var deviceNode = new Topology.DeviceNode(device);
                            parentNode.devices.Add(deviceNode);
                            deviceNode.parentNode = parentNode;

                            Topology.allService[device.id] = device;
                            deviceCount++;

                            LogManager.Log("添加设备", $"节点 {parentNodeId} 添加设备: {device.name}");
                        }
                    }
                    else
                    {
                        LogManager.Log("设备错误", $"设备 {deviceId} 的父节点 {parentNodeId} 不存在");
                    }
                }

                LogManager.Log("重建完成", $"成功构建 {nodeMap.Count - 1} 个节点, {deviceCount} 个设备");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Log("重建错误", $"重建拓扑结构失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        #endregion 加载功能

        #region 反序列化辅助方法

        private static Topology.LineNode CreateLineNodeFromString(string data)
        {
            try
            {
                var parts = data.Split('|');
                if (parts.Length < 5)
                {
                    LogManager.Log("节点错误", $"节点数据格式错误: {data}");
                    return null;
                }

                var node = new Topology.LineNode
                {
                    name = UnescapeString(parts[0]),
                    nodeSwitch = bool.Parse(parts[1]),
                    maxPower = double.Parse(parts[2]),
                    type = (NodeType)Enum.Parse(typeof(NodeType), parts[3]),
                    isOverLoad = bool.Parse(parts[4]),
                    isPowered = parts.Length > 5 ? bool.Parse(parts[5]) : true
                };

                return node;
            }
            catch (Exception ex)
            {
                LogManager.Log("重建错误", $"创建LineNode失败: {data}, 错误: {ex.Message}");
                return null;
            }
        }

        private static BaseDevice CreateDeviceFromString(string deviceStr)
        {
            try
            {
                var parts = deviceStr.Split('|');
                if (parts.Length < 6)
                {
                    LogManager.Log("设备错误", $"设备数据格式错误: {deviceStr}");
                    return null;
                }

                string name = UnescapeString(parts[1]);
                bool switchon = bool.Parse(parts[2]);
                double performance = double.Parse(parts[3]);
                NodeType type = (NodeType)Enum.Parse(typeof(NodeType), parts[4]);
                string deviceType = parts[5];

                BaseDevice device = null;

                if (deviceType == "BaseConsumer" && parts.Length >= 8)
                {
                    device = new BaseConsumer(name, performance, double.Parse(parts[6]));
                }
                else if (deviceType == "BaseProducer" && parts.Length >= 8)
                {
                    device = new BaseProducer(name, performance, double.Parse(parts[6]));
                }
                else if (deviceType == "BaseBattery" && parts.Length >= 9)
                {
                    var battery = new BaseBattery(name, performance, double.Parse(parts[6]));
                    battery.currentCharge = double.Parse(parts[7]);
                    battery.usingbattery = parts.Length > 8 ? bool.Parse(parts[8]) : false;
                    device = battery;
                }
                else
                {
                    LogManager.Log("设备错误", $"未知设备类型: {deviceType}");
                    return null;
                }

                if (device != null)
                {
                    device.switchon = switchon;
                    device.performanceRating = performance;
                }

                return device;
            }
            catch (Exception ex)
            {
                LogManager.Log("重建错误", $"创建设备失败: {deviceStr}, 错误: {ex.Message}");
                return null;
            }
        }

        private static string UnescapeString(string str)
        {
            return str?.Replace("||", "|").Replace("|,", ",") ?? "";
        }

        #endregion 反序列化辅助方法

        #region 验证和工具方法

        // 加载最新保存的文件
        public static bool LoadLatest()
        {
            var saveFiles = SaveManager.GetSaveFiles();
            if (saveFiles.Count == 0)
            {
                LogManager.Log("加载错误", "未找到保存文件");
                return false;
            }

            return Load(saveFiles.First());
        }

        // 验证拓扑结构
        private static void ValidateTopology()
        {
            try
            {
                int nodeCount = CountNodes(Topology.root);
                int deviceCount = CountDevices(Topology.root);

                LogManager.Log("拓扑验证", $"节点总数: {nodeCount}, 设备总数: {deviceCount}");

                var deviceIds = Topology.allService.Keys.ToList();
                var distinctIds = deviceIds.Distinct().Count();
                if (deviceIds.Count != distinctIds)
                {
                    LogManager.Log("验证警告", $"发现重复设备ID: 总数{deviceIds.Count}, 去重后{distinctIds}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Log("验证错误", $"拓扑验证失败: {ex.Message}");
            }
        }

        private static int CountNodes(Topology.LineNode node)
        {
            int count = 1;
            foreach (var child in node.childrenNode)
            {
                count += CountNodes(child);
            }
            return count;
        }

        private static int CountDevices(Topology.LineNode node)
        {
            int count = node.devices.Count;
            foreach (var child in node.childrenNode)
            {
                count += CountDevices(child);
            }
            return count;
        }

        #endregion 验证和工具方法
    }
}