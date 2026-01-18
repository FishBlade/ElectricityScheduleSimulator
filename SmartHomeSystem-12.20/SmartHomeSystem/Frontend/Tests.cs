using SmartHomeSystem.Backend.Framework;
using SmartHomeSystem.Connection;
using System.Windows;
using static SmartHomeSystem.Backend.Framework.Topology;

namespace SmartHomeSystem.Frontend
{
    public static class ComplexTopologyTest
    {
        public static void CreateComplexTestTopology()
        {
            // 平均功率1000KW
            var root = Topology.root;
            root.maxPower = 1000; // 总容量1000kW（增大总容量）

            // 创建三条主线路 - 修正容量层级
            var mainLine1 = new Topology.LineNode { maxPower = 30, name = "办公区主线路" };
            var mainLine2 = new Topology.LineNode { maxPower = 40, name = "生产区主线路" };
            var mainLine3 = new Topology.LineNode { maxPower = 20, name = "生活区主线路" };

            root.Add(mainLine1);
            root.Add(mainLine2);
            root.Add(mainLine3);

            // 办公区子线路 - 修正容量（子线路要小于父线路）
            var officeLightingLine = new Topology.LineNode { maxPower = 15, name = "办公照明线路" };
            var officeEquipmentLine = new Topology.LineNode { maxPower = 12, name = "办公设备线路" };
            var serverRoomLine = new Topology.LineNode { maxPower = 10, name = "服务器机房线路" };

            mainLine1.Add(officeLightingLine);
            mainLine1.Add(officeEquipmentLine);
            mainLine1.Add(serverRoomLine);

            // 生产区子线路 - 修正容量
            var productionLine1 = new Topology.LineNode { maxPower = 18, name = "生产线A" };
            var productionLine2 = new Topology.LineNode { maxPower = 18, name = "生产线B" };
            var auxiliaryLine = new Topology.LineNode { maxPower = 10, name = "辅助设备线路" };

            mainLine2.Add(productionLine1);
            mainLine2.Add(productionLine2);
            mainLine2.Add(auxiliaryLine);

            // 生活区子线路 - 修正容量
            var livingRoomLine = new Topology.LineNode { maxPower = 12, name = "生活区线路" };
            var kitchenLine = new Topology.LineNode { maxPower = 8, name = "厨房线路" };
            var outdoorLine = new Topology.LineNode { maxPower = 6, name = "户外线路" };

            mainLine3.Add(livingRoomLine);
            mainLine3.Add(kitchenLine);
            mainLine3.Add(outdoorLine);

            // 办公照明线路 - 三级子线路（设置较小的容量以制造过载）
            var floor1Lighting = new Topology.LineNode { maxPower = 8, name = "一层照明" };
            var floor2Lighting = new Topology.LineNode { maxPower = 8, name = "二层照明" };
            var emergencyLighting = new Topology.LineNode { maxPower = 3, name = "应急照明" };

            officeLightingLine.Add(floor1Lighting);
            officeLightingLine.Add(floor2Lighting);
            officeLightingLine.Add(emergencyLighting);

            // 添加各种类型的设备（包含不同优先级）
            AddOfficeDevices(officeEquipmentLine);
            AddServerRoomDevices(serverRoomLine);
            AddProductionDevices(productionLine1, productionLine2);
            AddAuxiliaryDevices(auxiliaryLine);
            AddLivingDevices(livingRoomLine, kitchenLine, outdoorLine);
            AddLightingDevices(floor1Lighting, floor2Lighting, emergencyLighting);

            // 添加发电设备（太阳能、发电机等）
            AddPowerGenerationDevices(root, outdoorLine);

            // 打开所有节点
            root.Open();

            // 计算功率
            Topology.CalculateNodePower();

            System.Diagnostics.Debug.WriteLine("复杂测试拓扑构建完成");

            // 添加调试信息
            System.Diagnostics.Debug.WriteLine("=== 优先级分布统计 ===");
            PrintPriorityStatistics(root);
        }

        public static void RefreshTreeView()
        {
            // 刷新树视图
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Dispatcher.Invoke((Delegate)(() =>
                {
                    if (mainWindow.DataContext is MainViewModel viewModel)
                    {
                        viewModel.RefreshEntireTree();
                    }
                }));
            }
        }

        // 辅助方法：添加辅助设备
        private static void AddAuxiliaryDevices(LineNode auxiliaryLine)
        {
            var devices = new[]
            {
                new { Name = "通风系统", Power = 5.5, On = true, Priority = BaseNode.PowerPlan.Important },
                new { Name = "空压机", Power = 8.0, On = true, Priority = BaseNode.PowerPlan.Normal },
                new { Name = "水泵", Power = 4.5, On = false, Priority = BaseNode.PowerPlan.Low },
                new { Name = "照明塔", Power = 2.0, On = true, Priority = BaseNode.PowerPlan.Normal }
            };

            foreach (var device in devices)
            {
                var consumer = new BaseConsumer
                {
                    name = device.Name,
                    powerConsumption = device.Power,
                    switchon = device.On
                };
                consumer.ConsumerformanceUpdate();
                var deviceNode = new DeviceNode(consumer)
                {
                    powerplan = device.Priority
                };
                auxiliaryLine.Add(deviceNode);
            }
        }

        // 辅助方法：添加照明设备
        private static void AddLightingDevices(LineNode floor1, LineNode floor2, LineNode emergency)
        {
            // 正常照明 - 普通优先级
            for (int i = 1; i <= 20; i++)
            {
                var light = new BaseConsumer
                {
                    name = $"照明灯{i}",
                    powerConsumption = 0.05,
                    switchon = i <= 15 // 只开75%的灯
                };
                light.ConsumerformanceUpdate();
                var deviceNode = new DeviceNode(light)
                {
                    powerplan = BaseNode.PowerPlan.Normal
                };
                floor1.Add(deviceNode);
            }

            for (int i = 1; i <= 25; i++)
            {
                var light = new BaseConsumer
                {
                    name = $"照明灯{i + 20}",
                    powerConsumption = 0.05,
                    switchon = true
                };
                light.ConsumerformanceUpdate();
                var deviceNode = new DeviceNode(light)
                {
                    powerplan = BaseNode.PowerPlan.Normal
                };
                floor2.Add(deviceNode);
            }

            // 应急照明 - 关键优先级
            for (int i = 1; i <= 8; i++)
            {
                var emergencyLight = new BaseConsumer
                {
                    name = $"应急灯{i}",
                    powerConsumption = 0.02,
                    switchon = true
                };
                emergencyLight.ConsumerformanceUpdate();
                var deviceNode = new DeviceNode(emergencyLight)
                {
                    powerplan = BaseNode.PowerPlan.Critical
                };
                emergency.Add(deviceNode);
            }
        }

        // 辅助方法：添加生活设备
        private static void AddLivingDevices(LineNode livingLine, LineNode kitchenLine, LineNode outdoorLine)
        {
            // 生活区设备 - 低优先级
            var livingDevices = new[]
            {
                new { Name = "电视", Power = 0.2, Count = 3, Priority = BaseNode.PowerPlan.Low },
                new { Name = "冰箱", Power = 1.5, Count = 2, Priority = BaseNode.PowerPlan.Normal },
                new { Name = "洗衣机", Power = 2.0, Count = 1, Priority = BaseNode.PowerPlan.Low }
            };

            foreach (var device in livingDevices)
            {
                for (int i = 0; i < device.Count; i++)
                {
                    var consumer = new BaseConsumer
                    {
                        name = $"{device.Name}_{i + 1}",
                        powerConsumption = device.Power,
                        switchon = true
                    };
                    consumer.ConsumerformanceUpdate();
                    var deviceNode = new DeviceNode(consumer)
                    {
                        powerplan = device.Priority
                    };
                    livingLine.Add(deviceNode);
                }
            }

            // 厨房设备 - 普通优先级
            var kitchenDevices = new[]
            {
                new { Name = "微波炉", Power = 1.2, On = false, Priority = BaseNode.PowerPlan.Normal },
                new { Name = "电饭煲", Power = 0.8, On = false, Priority = BaseNode.PowerPlan.Low },
                new { Name = "电磁炉", Power = 2.0, On = false, Priority = BaseNode.PowerPlan.Normal },
                new { Name = "抽油烟机", Power = 0.3, On = true, Priority = BaseNode.PowerPlan.Normal }
            };

            foreach (var device in kitchenDevices)
            {
                var consumer = new BaseConsumer
                {
                    name = device.Name,
                    powerConsumption = device.Power,
                    switchon = device.On
                };
                consumer.ConsumerformanceUpdate();
                var deviceNode = new DeviceNode(consumer)
                {
                    powerplan = device.Priority
                };
                kitchenLine.Add(deviceNode);
            }

            // 户外设备 - 重要优先级（安防相关）
            var outdoorDevices = new[]
            {
                new { Name = "景观灯", Power = 0.1, Count = 10, Priority = BaseNode.PowerPlan.Low },
                new { Name = "监控摄像头", Power = 0.05, Count = 8, Priority = BaseNode.PowerPlan.Important },
                new { Name = "电动门", Power = 1.0, Count = 1, Priority = BaseNode.PowerPlan.Important }
            };

            foreach (var device in outdoorDevices)
            {
                for (int i = 0; i < device.Count; i++)
                {
                    var consumer = new BaseConsumer
                    {
                        name = $"{device.Name}_{i + 1}",
                        powerConsumption = device.Power,
                        switchon = true
                    };
                    consumer.ConsumerformanceUpdate();
                    var deviceNode = new DeviceNode(consumer)
                    {
                        powerplan = device.Priority
                    };
                    outdoorLine.Add(deviceNode);
                }
            }
        }

        // 辅助方法：添加办公设备
        private static void AddOfficeDevices(LineNode officeLine)
        {
            var devices = new[]
            {
                new { Name = "电脑1", Power = 0.3, Count = 20, On = true, Priority = BaseNode.PowerPlan.Normal },
                new { Name = "打印机", Power = 0.5, Count = 5, On = true, Priority = BaseNode.PowerPlan.Normal },
                new { Name = "空调", Power = 2.5, Count = 8, On = true, Priority = BaseNode.PowerPlan.Important },
                new { Name = "饮水机", Power = 1.2, Count = 3, On = false, Priority = BaseNode.PowerPlan.Low }
            };

            foreach (var device in devices)
            {
                for (int i = 0; i < device.Count; i++)
                {
                    var consumer = new BaseConsumer
                    {
                        name = $"{device.Name}_{i + 1}",
                        powerConsumption = device.Power,
                        switchon = device.On
                    };
                    consumer.ConsumerformanceUpdate();
                    var deviceNode = new DeviceNode(consumer)
                    {
                        powerplan = device.Priority
                    };
                    officeLine.Add(deviceNode);
                }
            }
        }

        // 辅助方法：添加发电设备
        private static void AddPowerGenerationDevices(LineNode root, LineNode outdoorLine)
        {
            // 主太阳能系统 - 关键优先级
            var solarSystem = new LineNode { maxPower = 50, name = "太阳能发电系统" };
            root.Add(solarSystem);

            for (int i = 1; i <= 2; i++)
            {
                var solarPanel = new BaseProducer
                {
                    name = $"太阳能板阵列{i}",
                    powerProduction = 6.0 + (i * 0.5), // 不同发电量
                    switchon = true
                };
                solarPanel.ProducerformanceUpdate();
                var deviceNode = new DeviceNode(solarPanel)
                {
                    powerplan = BaseNode.PowerPlan.Critical
                };
                solarSystem.Add(deviceNode);
            }

            // 小型风力发电 - 重要优先级
            var windTurbine = new BaseProducer
            {
                name = "风力发电机",
                powerProduction = 5.0,
                switchon = true
            };
            windTurbine.ProducerformanceUpdate();
            var windDeviceNode = new DeviceNode(windTurbine)
            {
                powerplan = BaseNode.PowerPlan.Important
            };
            outdoorLine.Add(windDeviceNode);
        }

        // 辅助方法：添加生产设备
        private static void AddProductionDevices(LineNode line1, LineNode line2)
        {
            // 生产线A设备 - 混合优先级
            var productionDevicesA = new[]
            {
                new { Name = "数控机床", Power = 8.0, Count = 3, Priority = BaseNode.PowerPlan.Important },
                new { Name = "传送带", Power = 2.2, Count = 2, Priority = BaseNode.PowerPlan.Normal },
                new { Name = "机械臂", Power = 4.0, Count = 2, Priority = BaseNode.PowerPlan.Important },
                new { Name = "大型设备", Power = 4.0, Count = 10, Priority = BaseNode.PowerPlan.Normal }
            };

            foreach (var device in productionDevicesA)
            {
                for (int i = 0; i < device.Count; i++)
                {
                    var consumer = new BaseConsumer
                    {
                        name = $"{device.Name}A_{i + 1}",
                        powerConsumption = device.Power,
                        switchon = i < device.Count - 1 // 最后一个设备关闭
                    };
                    consumer.ConsumerformanceUpdate();
                    var deviceNode = new DeviceNode(consumer)
                    {
                        powerplan = device.Priority
                    };
                    line1.Add(deviceNode);
                }
            }

            // 生产线B设备 - 关键和重要优先级
            var productionDevicesB = new[]
            {
                new { Name = "注塑机", Power = 10.0, Count = 2, Priority = BaseNode.PowerPlan.Critical },
                new { Name = "包装机", Power = 3.5, Count = 1, Priority = BaseNode.PowerPlan.Important },
                new { Name = "检测设备", Power = 1.8, Count = 3, Priority = BaseNode.PowerPlan.Important }
            };

            foreach (var device in productionDevicesB)
            {
                for (int i = 0; i < device.Count; i++)
                {
                    var consumer = new BaseConsumer
                    {
                        name = $"{device.Name}B_{i + 1}",
                        powerConsumption = device.Power,
                        switchon = true
                    };
                    consumer.ConsumerformanceUpdate();
                    var deviceNode = new DeviceNode(consumer)
                    {
                        powerplan = device.Priority
                    };
                    line2.Add(deviceNode);
                }
            }
        }

        // 辅助方法：添加服务器机房设备
        private static void AddServerRoomDevices(LineNode serverRoomLine)
        {
            var serverRack1 = new LineNode { maxPower = 30, name = "服务器机架A" };
            var serverRack2 = new LineNode { maxPower = 30, name = "服务器机架B" };

            serverRoomLine.Add(serverRack1);
            serverRoomLine.Add(serverRack2);

            // 服务器设备 - 关键优先级
            for (int i = 1; i <= 8; i++)
            {
                var server = new BaseConsumer
                {
                    name = $"服务器{i}",
                    powerConsumption = 1.5 + (i * 0.1), // 不同功率
                    switchon = true
                };
                server.ConsumerformanceUpdate();
                var deviceNode = new DeviceNode(server)
                {
                    powerplan = BaseNode.PowerPlan.Critical
                };
                serverRack1.Add(deviceNode);
            }

            // 备份服务器 - 重要优先级
            for (int i = 1; i <= 6; i++)
            {
                var server = new BaseConsumer
                {
                    name = $"备份服务器{i}",
                    powerConsumption = 1.2,
                    switchon = i <= 3 // 只开一半
                };
                server.ConsumerformanceUpdate();
                var deviceNode = new DeviceNode(server)
                {
                    powerplan = BaseNode.PowerPlan.Important
                };
                serverRack2.Add(deviceNode);
            }

            // 机房空调 - 重要优先级
            var ac = new BaseConsumer
            {
                name = "机房精密空调",
                powerConsumption = 8.0,
                switchon = true
            };
            ac.ConsumerformanceUpdate();
            var acDeviceNode = new DeviceNode(ac)
            {
                powerplan = BaseNode.PowerPlan.Important
            };
            serverRoomLine.Add(acDeviceNode);
        }

        private static void CountPriorities(LineNode node, Dictionary<BaseNode.PowerPlan, int> counts)
        {
            foreach (var device in node.devices)
            {
                counts[device.powerplan]++;
            }

            foreach (var child in node.childrenNode)
            {
                CountPriorities(child, counts);
            }
        }

        // 辅助方法：打印优先级统计信息
        private static void PrintPriorityStatistics(LineNode node)
        {
            var priorityCounts = new Dictionary<BaseNode.PowerPlan, int>
            {
                { BaseNode.PowerPlan.Critical, 0 },
                { BaseNode.PowerPlan.Important, 0 },
                { BaseNode.PowerPlan.Normal, 0 },
                { BaseNode.PowerPlan.Low, 0 }
            };

            CountPriorities(node, priorityCounts);

            System.Diagnostics.Debug.WriteLine($"关键设备: {priorityCounts[BaseNode.PowerPlan.Critical]} 个");
            System.Diagnostics.Debug.WriteLine($"重要设备: {priorityCounts[BaseNode.PowerPlan.Important]} 个");
            System.Diagnostics.Debug.WriteLine($"普通设备: {priorityCounts[BaseNode.PowerPlan.Normal]} 个");
            System.Diagnostics.Debug.WriteLine($"低优先级设备: {priorityCounts[BaseNode.PowerPlan.Low]} 个");
        }
    }
}