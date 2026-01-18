using SmartHomeSystem.Backend.Framework;
using SmartHomeSystem.Backend.Function;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using static SmartHomeSystem.Backend.Framework.Topology;

namespace SmartHomeSystem.Frontend.ViewModels
{
    public abstract class NotifyObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
            return false;
        }
    }

    public class CycleGroup : NotifyObject
    {
        private int _cycleIndex;
        // 优化：不再频繁替换集合，而是重用
        private readonly ObservableCollection<SuspendQueueDisplayItem> _devices;

        public CycleGroup(int index)
        {
            _cycleIndex = index;
            _devices = new ObservableCollection<SuspendQueueDisplayItem>();

            // 监听集合变化自动更新计数，避免外部手动维护
            _devices.CollectionChanged += (s, e) => OnPropertyChanged(nameof(DeviceCount));
        }

        // XAML 使用 OneWay 绑定，不需要 set
        public string CycleDescription => _cycleIndex switch
        {
            0 => "关键优先级设备",
            1 => "重要优先级设备",
            2 => "普通优先级设备",
            3 => "低优先级设备",
            _ => "未知优先级"
        };

        public int CycleIndex
        {
            get => _cycleIndex;
            set
            {
                if (SetProperty(ref _cycleIndex, value, nameof(CycleIndex)))
                {
                    OnPropertyChanged(nameof(CycleTitle));
                    OnPropertyChanged(nameof(CycleDescription));
                }
            }
        }

        // 优化字符串拼接
        public string CycleTitle => $"优先级{_cycleIndex}";

        public int DeviceCount => _devices.Count;

        public ObservableCollection<SuspendQueueDisplayItem> Devices => _devices;
    }

    // 已移除 XAML 未使用的 OverloadNodeDisplayItem 类

    public class SchedulingStatus : NotifyObject
    {
        private double _currentElectricityPrice;
        private double _currentRecoveryRatio;
        private int _overloadNodeCount;
        private int _recoveryCycleCounter;
        private int _suspendedDeviceCount;

        public double CurrentElectricityPrice
        {
            get => _currentElectricityPrice;
            set => SetProperty(ref _currentElectricityPrice, value, nameof(CurrentElectricityPrice));
        }

        public double CurrentRecoveryRatio
        {
            get => _currentRecoveryRatio;
            set => SetProperty(ref _currentRecoveryRatio, value, nameof(CurrentRecoveryRatio));
        }

        public int OverloadNodeCount
        {
            get => _overloadNodeCount;
            set => SetProperty(ref _overloadNodeCount, value, nameof(OverloadNodeCount));
        }

        public int RecoveryCycleCounter
        {
            get => _recoveryCycleCounter;
            set => SetProperty(ref _recoveryCycleCounter, value, nameof(RecoveryCycleCounter));
        }

        public int SuspendedDeviceCount
        {
            get => _suspendedDeviceCount;
            set => SetProperty(ref _suspendedDeviceCount, value, nameof(SuspendedDeviceCount));
        }
    }

    public class SuspendQueueDisplayItem : NotifyObject
    {
        // 仅保留 XAML 绑定的属性背后的字段
        private string _deviceName;
        private string _powerConsumption;
        private int _priorityLevel;
        private string _recoveryChance;
        private int _recoveryCycle;
        private string _waitTime;

        // 缓存后端节点用于比较，但不直接绑定
        public DeviceNode BackendNode { get; set; }

        public string DeviceName
        {
            get => _deviceName;
            set => SetProperty(ref _deviceName, value, nameof(DeviceName));
        }

        public string PowerConsumption
        {
            get => _powerConsumption;
            set => SetProperty(ref _powerConsumption, value, nameof(PowerConsumption));
        }

        public int PriorityLevel
        {
            get => _priorityLevel;
            set => SetProperty(ref _priorityLevel, value, nameof(PriorityLevel));
        }

        public string RecoveryChance
        {
            get => _recoveryChance;
            set => SetProperty(ref _recoveryChance, value, nameof(RecoveryChance));
        }

        public int RecoveryCycle
        {
            get => _recoveryCycle;
            set => SetProperty(ref _recoveryCycle, value, nameof(RecoveryCycle));
        }

        public string WaitTime
        {
            get => _waitTime;
            set => SetProperty(ref _waitTime, value, nameof(WaitTime));
        }
    }

    public class SuspendQueue : NotifyObject
    {
        #region 属性
        private string _currentRecoveryInfo = "系统准备就绪";
        private SchedulingStatus _schedulingStatus;

        // 优化：固定集合，只初始化一次
        private readonly ObservableCollection<CycleGroup> _suspendQueueByCycle;

        // 兼容性保留，但不再主动更新以节省性能
        private ObservableCollection<SuspendQueueDisplayItem> _suspendQueueItems;

        public SuspendQueue()
        {
            _schedulingStatus = new SchedulingStatus();
            _suspendQueueItems = new ObservableCollection<SuspendQueueDisplayItem>();

            // 初始化固定的4个周期组
            _suspendQueueByCycle = new ObservableCollection<CycleGroup>();
            for (int i = 0; i < 4; i++)
            {
                _suspendQueueByCycle.Add(new CycleGroup(i));
            }
        }

        public string CurrentRecoveryInfo
        {
            get => _currentRecoveryInfo;
            set => SetProperty(ref _currentRecoveryInfo, value, nameof(CurrentRecoveryInfo));
        }

        public SchedulingStatus SchedulingStatus
        {
            get => _schedulingStatus;
            set => SetProperty(ref _schedulingStatus, value, nameof(SchedulingStatus));
        }

        public ObservableCollection<CycleGroup> SuspendQueueByCycle => _suspendQueueByCycle;

        [Obsolete("使用 SuspendQueueByCycle 替代")]
        public ObservableCollection<SuspendQueueDisplayItem> SuspendQueueItems => _suspendQueueItems;

        // XAML 中未使用的 OverloadNodes 属性已移除
        #endregion 属性

        #region 方法

        // UI刷新入口
        public void UpdateQueuesDisplay()
        {
            // 优化：线程安全检查
            // 如果从后台线程调用（如 Time 事件），强制切换到 UI 线程
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(UpdateQueuesDisplay));
                return;
            }

            try
            {
                UpdateSchedulingStatus();
                UpdateSuspendQueueByCycle();
                UpdateRecoveryInfo();

                // 仅通知必要属性
                // SuspendQueueByCycle 集合引用本身没变，不需要 Notify
                // SchedulingStatus 引用没变，不需要 Notify (其内部属性变了会自动通知)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新队列显示失败: {ex.Message}");
            }
        }

        private void UpdateRecoveryInfo()
        {
            double currentPrice = ElectricityPricing.GetCurrentPrice();
            // 缓存计算结果
            double recoveryRatio = Schedule.RecoveryRatio;
            int currentCycle = Schedule.GetCurrentRecoveryCycle();

            CurrentRecoveryInfo =
                $"当前电价: {currentPrice:F2}元/KWh | " +
                $"恢复比例: {recoveryRatio:P0} | " +
                $"周期: {currentCycle % Schedule.RECOVERY_LIMIT + 1}/{Schedule.RECOVERY_LIMIT}";
        }

        private void UpdateSchedulingStatus()
        {
            // 直接更新现有对象的属性，避免创建新对象
            var s = SchedulingStatus;
            s.CurrentRecoveryRatio = Schedule.RecoveryRatio;
            s.RecoveryCycleCounter = Schedule.GetCurrentRecoveryCycle();
            s.OverloadNodeCount = Schedule.GetOverloadedNodes().Count();
            s.SuspendedDeviceCount = Schedule.GetSuspendedDevices().Sum(g => g.Value?.Count() ?? 0);
            s.CurrentElectricityPrice = ElectricityPricing.GetCurrentPrice();
        }

        private void UpdateSuspendQueueByCycle()
        {
            var suspendedDevicesMap = Schedule.GetSuspendedDevices();

            // 遍历4个优先级组 (对应 XAML 中的4个周期组)
            for (int i = 0; i < 4; i++)
            {
                var targetGroup = _suspendQueueByCycle[i];
                var sourceDevices = suspendedDevicesMap.ContainsKey(i)
                    ? suspendedDevicesMap[i].ToList()
                    : new List<DeviceNode>();

                SyncDeviceList(targetGroup.Devices, sourceDevices, i);
            }
        }

        /// <summary>
        /// 智能同步列表，减少 UI 刷新闪烁
        /// </summary>
        private void SyncDeviceList(ObservableCollection<SuspendQueueDisplayItem> target, List<DeviceNode> source, int cycleIndex)
        {
            // 如果数量不匹配或源为空，直接重置可能更快且安全
            if (source.Count == 0)
            {
                if (target.Count > 0) target.Clear();
                return;
            }

            // 简单同步策略：
            // 为了性能，且考虑到挂起队列变动较快，这里采用：
            // 1. 移除多余的
            while (target.Count > source.Count)
            {
                target.RemoveAt(target.Count - 1);
            }

            // 2. 更新或添加
            for (int i = 0; i < source.Count; i++)
            {
                var deviceNode = source[i];

                if (i < target.Count)
                {
                    // 更新现有项
                    UpdateItem(target[i], deviceNode, cycleIndex, i);
                }
                else
                {
                    // 添加新项
                    var newItem = new SuspendQueueDisplayItem();
                    UpdateItem(newItem, deviceNode, cycleIndex, i);
                    target.Add(newItem);
                }
            }
        }

        private void UpdateItem(SuspendQueueDisplayItem item, DeviceNode node, int cycleIndex, int index)
        {
            if (node.currentDevice == null) return;

            // 只有当引用不同或关键数据变化时才写入属性（NotifyObject 内部有检查）
            item.BackendNode = node;
            item.DeviceName = node.currentDevice.name ?? "未命名";
            item.PriorityLevel = cycleIndex; // 简化逻辑：cycleIndex 即 priority
            item.RecoveryCycle = cycleIndex;

            // 动态计算值
            double power = node.currentDevice switch
            {
                Consumer c => c.realpowerConsumption,
                Producer p => p.realpowerProduction,
                _ => 0
            };
            item.PowerConsumption = $"{power:F1}KW";

            // 辅助显示逻辑
            item.WaitTime = CalculateWaitTime(cycleIndex, cycleIndex); // 保持原有逻辑
            item.RecoveryChance = CalculateRecoveryChance(cycleIndex);
        }

        private string CalculateWaitTime(int priority, int cycleIndex)
        {
            int baseWait = (3 - priority) * 2;
            int totalWait = baseWait + cycleIndex;
            return totalWait <= 0 ? "即将恢复" : $"{totalWait}周期后";
        }

        private string CalculateRecoveryChance(int priority)
        {
            return priority switch
            {
                0 => "高",
                1 => "中",
                2 => "低",
                _ => "极低"
            };
        }

        #endregion 方法
    }
}