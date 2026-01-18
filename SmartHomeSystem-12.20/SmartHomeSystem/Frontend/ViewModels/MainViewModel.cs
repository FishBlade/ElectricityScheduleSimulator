using LiveCharts;
using SmartHomeSystem.Backend.Framework;
using SmartHomeSystem.Backend.Function;
using SmartHomeSystem.Frontend.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading; 
using static SmartHomeSystem.Backend.Framework.Topology;

namespace SmartHomeSystem.Connection
{
    public class RelayCommand : ICommand
    {
        private readonly Func<bool> _canExecute;
        private readonly Action _execute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }

    public partial class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        public static MainViewModel _instance; // 访问修饰符

        // 核心子ViewModel
        private readonly ChartData _chartDataManager;
        private readonly SuspendQueue _suspendQueue; // 统一命名规范
        private readonly ContextMenuViewModel _contextMenuViewModel;

        private TreeNode _selectedTreeNode;
        private ObservableCollection<TreeNode> _treeNodes;

        // --- 性能优化缓存字段 ---
        private double _cacheRootConsump;
        private double _cacheRootProd;
        private double _cacheTotalPrice;
        private double _cacheCurrentPrice;
        // -----------------------

        public MainViewModel()
        {
            _instance = this;
            _treeNodes = new ObservableCollection<TreeNode>();

            // 初始化子ViewModel
            _suspendQueue = new SuspendQueue();
            _chartDataManager = new ChartData();
            _contextMenuViewModel = new ContextMenuViewModel();

            // 订阅子组件事件
            _suspendQueue.PropertyChanged += (s, e) => OnSuspendQueuePropertyChanged(e.PropertyName);
            _chartDataManager.OnSeriesCollectionChanged += () => OnPropertyChanged(nameof(SeriesCollection));

            // 订阅后端事件
            Event.LowCostEvent += LowCostUpdate;
            Event.HighCostEvent += HighCostUpdate;
            Event.HourlyEvent += OnHourlyUpdate;
            // HourlyEvent += OnTopologyUpdate; // 移除重复订阅

            // 初始化命令
            ApplyNodeChangesCommand = new RelayCommand(ApplyNodeChanges, CanApplyNodeChanges);
            ApplyDeviceChangesCommand = new RelayCommand(ApplyDeviceChanges, CanApplyDeviceChanges);
            RefreshTreeCommand = new RelayCommand(RefreshEntireTree);
            RefreshDebugPanelCommand = new RelayCommand(RefreshDebugPanel, CanRefreshDebugPanel);

            InitializeTreeView();
        }

        public static MainViewModel Instance => _instance;
        public event PropertyChangedEventHandler PropertyChanged;

        #region 子ViewModel访问器

        public ContextMenuViewModel ContextMenuVM => _contextMenuViewModel;

        // 委托给 SuspendQueue
        public string CurrentRecoveryInfo => _suspendQueue.CurrentRecoveryInfo;
        public SchedulingStatus SchedulingStatus => _suspendQueue.SchedulingStatus;
        public ObservableCollection<CycleGroup> SuspendQueueByCycle => _suspendQueue.SuspendQueueByCycle;
        // 兼容性保留
        public ObservableCollection<SuspendQueueDisplayItem> SuspendQueueItems => _suspendQueue.SuspendQueueItems;

        // 委托给 ChartData
        public SeriesCollection SeriesCollection => _chartDataManager.SeriesCollection;
        public Func<double, string> YFormatter => _chartDataManager.YFormatter;

        #endregion

        #region TreeView相关属性

        public ObservableCollection<TreeNode> TreeNodes
        {
            get => _treeNodes;
            set { _treeNodes = value; OnPropertyChanged(nameof(TreeNodes)); }
        }

        public TreeNode SelectedTreeNode
        {
            get => _selectedTreeNode;
            set
            {
                if (_selectedTreeNode != value)
                {
                    _selectedTreeNode = value;
                    OnPropertyChanged(nameof(SelectedTreeNode));
                    // 切换节点时立即刷新面板
                    RefreshDebugPanel();
                }
            }
        }

        #endregion

        #region 命令定义

        public ICommand ApplyDeviceChangesCommand { get; }
        public ICommand ApplyNodeChangesCommand { get; }
        public ICommand RefreshDebugPanelCommand { get; }
        public ICommand RefreshTreeCommand { get; }

        #endregion

        #region 显示属性 (字符串格式化)

        // 使用 => 表达式，保证每次获取都是最新计算值
        public string DisplayRootCumulativeElectricityConsumption =>
            $"累计用电：{Topology.cumulativeElectricityConsumption:F2} KWh";

        public string DisplayRootCumulativeElectricityPricing =>
            $"累计电费：{Topology.cumulativeElectricityPricing:F2} 元";

        public string DisplayRootElectricityPricing =>
            $"当前电价：{ElectricityPricing.GetCurrentPrice():F2} 元/KWh";

        public string DisplayRootPowerConsumption =>
            $"用电功率：{Topology.root.PowerConsumption:F2} KW/h";

        public string DisplayRootPowerProduction =>
            $"发电功率：{Topology.root.PowerProduction:F2} KW/h";

        public string DisplayRootRealPower =>
            $"净功率：{(Topology.root.PowerConsumption - Topology.root.PowerProduction):F2} KW/h";

        public string DisplayTime => "时间：" + Time.GetStringTime();

        // 选中节点计算属性
        public string Consumption => GetConsumption();
        public string Durability => GetDurability();
        public string NodePower => GetPower();
        public string NodeType => GetNodeType();
        public string Statue => GetStatue();

        public string SelectedDeviceInfo => GetSelectedDeviceInfo();
        public string SelectedNodeInfo => GetSelectedNodeInfo();

        public bool IsConsumerVisible => Frontend.FrontContext.selectNode is DeviceNode { currentDevice: BaseConsumer };
        public bool IsProducerVisible => Frontend.FrontContext.selectNode is DeviceNode { currentDevice: BaseProducer };

        #endregion

        #region 逻辑交互

        public void OnTreeNodeRightClicked(TreeNode treeNode)
        {
            if (treeNode?.BackendNode != null)
            {
                Frontend.FrontContext.selectNode = treeNode.BackendNode;
                SelectedTreeNode = treeNode;
                ContextMenuVM.SelectedNode = treeNode.BackendNode;
                RefreshDebugPanel();
            }
        }

        public void InitializeTreeView()
        {
            TreeNodes.Clear();
            if (Topology.root != null)
            {
                var rootVM = new TreeNode(Topology.root) { IsExpanded = true, IsSelected = true };
                TreeNodes.Add(rootVM);
                Frontend.FrontContext.selectNode = Topology.root;
                UpdateEditorValues();
            }
        }

        public void RefreshEntireTree()
        {
            RunOnUI(() =>
            {
                TreeNode.SyncTreeWithBackend(TreeNodes, Topology.root);
                OnPropertyChanged(nameof(TreeNodes));
            });
        }

        // 轻量刷新
        public void RefreshTreeDataOnly()
        {
            RunOnUI(() =>
            {
                TreeNode.RefreshTreeDataOnly(TreeNodes);
                NotifySelectedNodeStats();
            });
        }

        private void SyncTreeWithBackend() => TreeNode.SyncTreeWithBackend(TreeNodes, Topology.root);

        // 委托给管理类，并在UI线程更新
        public void UpdateQueuesDisplay()
        {
            RunOnUI(() => _suspendQueue.UpdateQueuesDisplay());
        }

        public void RefreshDebugPanel()
        {
            try
            {
                UpdateEditorValues();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新面板失败: {ex.Message}");
            }
        }

        private bool CanApplyDeviceChanges() => Frontend.FrontContext.selectNode is DeviceNode dn && dn.currentDevice != null;
        private bool CanApplyNodeChanges() => Frontend.FrontContext.selectNode != null;
        private bool CanRefreshDebugPanel() => true;

        #endregion

        #region 事件处理 (核心优化区)

        private void LowCostUpdate()
        {
            // 线程安全 + 性能节流
            RunOnUI(() =>
            {
                // 1. 总是更新时间
                OnPropertyChanged(nameof(DisplayTime));

                // 2. 根节点数据节流 (只有变化 > 0.01 才更新UI)
                CheckAndNotify(ref _cacheRootConsump, Topology.root.PowerConsumption,
                    nameof(DisplayRootPowerConsumption), nameof(DisplayRootRealPower));

                CheckAndNotify(ref _cacheRootProd, Topology.root.PowerProduction,
                    nameof(DisplayRootPowerProduction), nameof(DisplayRootRealPower));

                CheckAndNotify(ref _cacheCurrentPrice, ElectricityPricing.GetCurrentPrice(),
                    nameof(DisplayRootElectricityPricing));

                // 累计值通常一直在变，直接更新
                OnPropertyChanged(nameof(DisplayRootCumulativeElectricityConsumption));
                OnPropertyChanged(nameof(DisplayRootCumulativeElectricityPricing));

                // 3. 刷新选中节点的实时状态
                NotifySelectedNodeStats();
            });
        }

        private void HighCostUpdate()
        {
            // 队列和图表属于低频更新
            RunOnUI(UpdateQueuesDisplay);
        }

        private void OnHourlyUpdate()
        {
            RunOnUI(SyncTreeWithBackend);
        }

        private void OnSuspendQueuePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        #endregion

        #region 辅助方法

        // 安全地在UI线程执行
        private void RunOnUI(Action action)
        {
            if (Application.Current?.Dispatcher == null) return;

            if (Application.Current.Dispatcher.CheckAccess())
                action();
            else
                Application.Current.Dispatcher.BeginInvoke(action);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 节流辅助函数
        private void CheckAndNotify(ref double cache, double newValue, params string[] propertyNames)
        {
            if (Math.Abs(cache - newValue) > 0.01)
            {
                cache = newValue;
                foreach (var name in propertyNames) OnPropertyChanged(name);
            }
        }

        private void NotifySelectedNodeStats()
        {
            OnPropertyChanged(nameof(Consumption));
            OnPropertyChanged(nameof(Durability));
            OnPropertyChanged(nameof(NodePower));
            OnPropertyChanged(nameof(NodeType));
            OnPropertyChanged(nameof(Statue));
        }

        // 静态文本获取方法 (直接内联逻辑，减少方法跳转)
        public static string GetConsumption() => Frontend.FrontContext.selectNode switch
        {
            LineNode ln => $"消耗: {ln.PowerConsumption:F2} KW/h",
            DeviceNode { currentDevice: BaseConsumer c } => $"消耗: {c.realpowerConsumption:F2} KW/h",
            DeviceNode { currentDevice: BaseProducer p } => $"生产: {p.realpowerProduction:F2} KW/h",
            _ => "消耗: 0.00"
        };

        public static string GetDurability() => Frontend.FrontContext.selectNode is DeviceNode dn
            ? $"{(dn.currentDevice.performanceRating * 100):F2}%"
            : "0.00%";

        public static string GetNodeType() => Frontend.FrontContext.selectNode?.type.ToString() ?? "未选择节点";

        public static string GetPower()
        {
            if (Frontend.FrontContext.selectNode is LineNode { maxPower: > 0 } ln)
            {
                return $"{(ln.PowerConsumption / ln.maxPower * 100):F2}%";
            }
            return "0.00%";
        }

        public static string GetStatue()
        {
            var node = Frontend.FrontContext.selectNode;
            if (node == null) return "状态: 未知";

            string s = node.nodeSwitch ? "运行中" : "已关闭";
            if (node is LineNode ln && ln.isOverLoad) s += " (过载)";
            return $"状态: {s}";
        }

        private string GetSelectedDeviceInfo()
        {
            if (Frontend.FrontContext.selectNode is DeviceNode dn && dn.currentDevice != null)
                return $"{dn.currentDevice.GetType().Name} - {(dn.currentDevice.switchon ? "运行中" : "已关闭")}";
            return "无设备或未选择设备节点";
        }

        private string GetSelectedNodeInfo()
        {
            var node = Frontend.FrontContext.selectNode;
            if (node == null) return "未选择节点";

            string s = $"{node.type} - {(node.nodeSwitch ? "运行中" : "已关闭")}";
            if (node is LineNode ln && ln.isOverLoad) s += " (过载)";
            return $"状态: {s}";
        }

        #endregion

        public void Dispose()
        {
            Event.HighCostEvent -= HighCostUpdate;
            Event.LowCostEvent -= LowCostUpdate;
            Event.HourlyEvent -= OnHourlyUpdate;

            // 确保释放 ChartData 资源
            _chartDataManager?.Dispose();
        }
    }
}